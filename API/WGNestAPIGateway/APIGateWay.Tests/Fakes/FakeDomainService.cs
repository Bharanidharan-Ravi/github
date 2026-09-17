using System.Linq.Expressions;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.ChatsModal.Master;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APIGateWay.Tests.Fakes
{
    /// <summary>Minimal EF Core InMemory-backed context — only the DbSets the Phase 1/4 tests touch.</summary>
    public class FakeChatDbContext : DbContext
    {
        public FakeChatDbContext(DbContextOptions<FakeChatDbContext> options) : base(options) { }

        public DbSet<ChatUserKey> ChatUserKeys => Set<ChatUserKey>();
        public DbSet<ChatUserKeyRecoveryEscrow> ChatUserKeyRecoveryEscrows => Set<ChatUserKeyRecoveryEscrow>();
        public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
        public DbSet<ChatConversationMember> ChatConversationMembers => Set<ChatConversationMember>();
        public DbSet<ChatEncryptedMessage> ChatEncryptedMessages => Set<ChatEncryptedMessage>();
        public DbSet<ChatMessageKey> ChatMessageKeys => Set<ChatMessageKey>();
        public DbSet<ChatMessageReaction> ChatMessageReactions => Set<ChatMessageReaction>();
        public DbSet<ChatMessageTag> ChatMessageTags => Set<ChatMessageTag>();
        public DbSet<ChatMediaAttachment> ChatMediaAttachments => Set<ChatMediaAttachment>();
        public DbSet<LOGIN_MASTER> LoginMasters => Set<LOGIN_MASTER>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ChatConversationMember>().HasKey(m => new { m.ConversationId, m.UserId });
            modelBuilder.Entity<ChatMessageKey>().HasKey(k => new { k.MessageId, k.RecipientUserId });
        }
    }

    /// <summary>
    /// Fakes just enough of IDomainService for ChatKeyRepo: Query, SaveEntityAsync, UpdateAsync,
    /// ExecuteInTransactionAsync. Backed by a fresh EF Core InMemory database per test.
    /// InMemory doesn't support real transactions, so ExecuteInTransactionAsync just runs the
    /// delegate directly — good enough for testing repo logic, not for testing rollback behavior.
    /// </summary>
    public class FakeDomainService : IDomainService
    {
        public readonly FakeChatDbContext Db;

        public FakeDomainService()
        {
            var options = new DbContextOptionsBuilder<FakeChatDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new FakeChatDbContext(options);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> businessLogic) => await businessLogic();

        public IQueryable<TEntity> Query<TEntity>() where TEntity : class => Db.Set<TEntity>();

        public async Task SaveEntityAsync<TEntity>(TEntity entity) where TEntity : class
        {
            Db.Set<TEntity>().Add(entity);
            await Db.SaveChangesAsync();
        }

        public async Task SaveEntitiesAsync<TEntity>(List<TEntity> entities) where TEntity : class
        {
            Db.Set<TEntity>().AddRange(entities);
            await Db.SaveChangesAsync();
        }

        public async Task UpdateAsync<T>(T entity) where T : class
        {
            Db.Set<T>().Update(entity);
            await Db.SaveChangesAsync();
        }

        public async Task UpdateEntitiesAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            Db.Set<TEntity>().UpdateRange(entities);
            await Db.SaveChangesAsync();
        }

        public async Task UpdateTrackedEntityAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, Action<TEntity> mutator) where TEntity : class
        {
            var entity = await Db.Set<TEntity>().FirstOrDefaultAsync(predicate)
                ?? throw new APIGateWay.ModelLayer.ErrorException.Exceptionlist.DataNotFoundException($"{typeof(TEntity).Name} not found.");
            mutator(entity);
            await Db.SaveChangesAsync();
        }

        public Task<TEntity> UpdateEntityWithAttachmentsAsync<TEntity>(object id, Action<TEntity> mutator, List<AttachmentMaster>? newAttachments = null) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task<TEntity> UpdateEntityByPredicateWithAttachmentsAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, Action<TEntity> mutator, List<AttachmentMaster>? newAttachments = null) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task UpdateLabelAsync(Guid id, List<IssueLabel> labels)
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task<TEntity> UpdateEntityByIntIdAsync<TEntity>(int id, Action<TEntity> mutator) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task SaveEntityWithAttachmentsAsync<TEntity>(TEntity entity, List<AttachmentMaster> attachments) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task SaveLabelAsync(List<IssueLabel> labels)
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        public Task SaveAttachmentsAsync(List<AttachmentMaster> attachments)
            => throw new NotImplementedException("Not used by ChatKeyRepo");

        /// <summary>
        /// Looks up the tracked instance by key instead of blindly attaching <paramref name="entity"/> —
        /// callers often pass a fresh (AsNoTracking-fetched, or key-only stub) instance while an earlier
        /// call in the same test already tracks a different instance with the same key, which EF Core's
        /// change tracker rejects as an identity conflict.
        /// </summary>
        public async Task DeleteEntityAsync<TEntity>(TEntity entity) where TEntity : class
        {
            var key = Db.Model.FindEntityType(typeof(TEntity))!.FindPrimaryKey()!;
            var keyValues = key.Properties.Select(p => p.PropertyInfo!.GetValue(entity)).ToArray();
            var tracked = await Db.Set<TEntity>().FindAsync(keyValues);
            if (tracked != null) Db.Set<TEntity>().Remove(tracked);
            await Db.SaveChangesAsync();
        }
    }

    public class FakeLoginContextService : ILoginContextService
    {
        public Guid userId { get; set; } = Guid.NewGuid();
        public string userName { get; set; } = "test-user";
        public string databaseName { get; set; } = "WGNEST";
        public string Status { get; set; } = "Active";
        public int role { get; set; } = 1;
        public string JwtToken { get; set; } = "fake-token";
        public string RequestPath { get; set; } = "/api/ChatKeys/me";
    }

    /// <summary>No-op step timer — ChatKeyRepo calls this for logging only.</summary>
    public class FakeRequestStepContext : IRequestStepContext
    {
        public System.Diagnostics.Stopwatch StartStep() => System.Diagnostics.Stopwatch.StartNew();
        public void Success(string tableName, string operation, string? insertedId, System.Diagnostics.Stopwatch timer) { }
        public void Failure(string tableName, string operation, string? errorMessage, string? innerException, System.Diagnostics.Stopwatch timer) { }
        public List<APIGateWay.ModalLayer.MasterData.ApiLogStep> GetSteps() => new();
    }

    /// <summary>Reversible tag instead of real crypto — enough to test that encrypt/decrypt round-trips.</summary>
    public class FakeChatRecoveryEscrowCipher : APIGateWay.BusinessLayer.Helpers.IChatRecoveryEscrowCipher
    {
        public string Encrypt(string plainText) => $"enc:{plainText}";
        public string Decrypt(string cipherText) => cipherText.StartsWith("enc:") ? cipherText[4..] : cipherText;
    }

    /// <summary>
    /// No-op SignalR hub context — ChatRepo only fires-and-forgets broadcasts, it never
    /// inspects the result, so the fake just has to not throw and let tests inspect `Sent`.
    /// </summary>
    public class FakeHubContext : IHubContext<RealtimeHub>
    {
        public readonly List<(string Group, string Method, object? Arg)> Sent = new();

        public IHubClients Clients => new FakeHubClients(this);
        public IGroupManager Groups => throw new NotImplementedException("Not used by ChatRepo");

        private class FakeHubClients : IHubClients
        {
            private readonly FakeHubContext _owner;
            public FakeHubClients(FakeHubContext owner) => _owner = owner;

            public IClientProxy All => throw new NotImplementedException();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
            public IClientProxy Client(string connectionId) => throw new NotImplementedException();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
            public IClientProxy Group(string groupName) => new FakeClientProxy(_owner, groupName);
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
            public IClientProxy OthersInGroup(string groupName) => throw new NotImplementedException();
            public IClientProxy User(string userId) => throw new NotImplementedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
        }

        private class FakeClientProxy : IClientProxy
        {
            private readonly FakeHubContext _owner;
            private readonly string _group;
            public FakeClientProxy(FakeHubContext owner, string group)
            {
                _owner = owner;
                _group = group;
            }

            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                _owner.Sent.Add((_group, method, args.Length > 0 ? args[0] : null));
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>No-op logger — ChatRepo only logs warnings on realtime-delivery failure.</summary>
    public class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
