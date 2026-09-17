using System.Linq.Expressions;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.Tests.Fakes
{
    /// <summary>Minimal EF Core InMemory-backed context — only the DbSets Phase 1 tests touch.</summary>
    public class FakeChatDbContext : DbContext
    {
        public FakeChatDbContext(DbContextOptions<FakeChatDbContext> options) : base(options) { }

        public DbSet<APIGateWay.ModalLayer.ChatsModal.Master.ChatUserKey> ChatUserKeys => Set<APIGateWay.ModalLayer.ChatsModal.Master.ChatUserKey>();
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

        public Task UpdateTrackedEntityAsync<TEntity>(Expression<Func<TEntity, bool>> predicate, Action<TEntity> mutator) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");

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

        public Task DeleteEntityAsync<TEntity>(TEntity entity) where TEntity : class
            => throw new NotImplementedException("Not used by ChatKeyRepo");
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
}
