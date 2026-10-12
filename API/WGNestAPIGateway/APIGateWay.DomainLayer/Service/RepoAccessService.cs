using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoAccessService : IRepoAccessService
    {
        // RepoScopeHandler asks for this on every role-3 request (heartbeat
        // included). Kept in memory per database + user; writes that change
        // membership call Forget, and the expiry covers edits made in SQL.
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        private readonly APIGatewayDBContext _dbContext;
        private readonly IMemoryCache _cache;

        public RepoAccessService(APIGatewayDBContext dBContext, IMemoryCache cache)
        {
            _dbContext = dBContext;
            _cache = cache;
        }

        // RepoKey strings — used by the SignalR hub and RepoScopeValidator
        public async Task<List<string>> GetUserRepoIdsAsync(Guid userId)
        {
            var repos = await GetCachedAsync(userId);
            return repos.Select(x => x.RepoKey).ToList();
        }

        // Repo_Id (GUID for SP @repoId param) and RepoKey of every repo the user belongs to
        public async Task<List<UserRepoAccess>> GetUserRepoGuidsAsync(Guid userId)
        {
            var repos = await GetCachedAsync(userId);
            return repos.ToList();
        }

        public void Forget(IEnumerable<Guid> userIds)
        {
            foreach (var userId in userIds.Distinct())
                _cache.Remove(CacheKey(userId));
        }

        public Task<string?> GetTicketRepoKeyAsync(Guid ticketId)
        {
            return _dbContext.ISSUEMASTER
                .Where(t => t.Issue_Id == ticketId)
                .Select(t => t.RepoKey)
                .FirstOrDefaultAsync();
        }

        // Callers get their own list; the cached array is never handed out
        private async Task<UserRepoAccess[]> GetCachedAsync(Guid userId)
        {
            return await _cache.GetOrCreateAsync(CacheKey(userId), entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return LoadAsync(userId);
            }) ?? Array.Empty<UserRepoAccess>();
        }

        private Task<UserRepoAccess[]> LoadAsync(Guid userId)
        {
            return (
                from repo in _dbContext.RepositoryMasters
                join repoUser in _dbContext.RepoUsers
                    on repo.RepoKey equals repoUser.RepoKey
                where repoUser.UserId == userId
                select new UserRepoAccess
                {
                    RepoId = (Guid)repo.Repo_Id,   // PK from RepositoryMasters
                    RepoKey = repo.RepoKey   // short key
                }
            ).ToArrayAsync();
        }

        // Server + database the request is routed to (tenant DB, Live/Test)
        private string CacheKey(Guid userId)
        {
            var conn = new SqlConnectionStringBuilder(_dbContext.Database.GetConnectionString());
            return $"repo-access:{conn.DataSource}:{conn.InitialCatalog}:{userId}";
        }
    }
}
