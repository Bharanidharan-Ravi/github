using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    public class RepoAccessService : IRepoAccessService
    {
        private readonly APIGatewayDBContext _dbContext;

        public RepoAccessService(APIGatewayDBContext dBContext)
        {
            _dbContext = dBContext;
        }
        // ── Already exists — used by SignalR hub ──────────────────────────────
        public async Task<List<string>> GetUserRepoIdsAsync(Guid userId)
        {
            return await _dbContext.RepoUsers
                .Where(x => x.UserId == userId)
                .Select(x => x.RepoKey)
                .ToListAsync();
        }

        // ── ADD THIS ──────────────────────────────────────────────────────────
        // Returns both Repo_Id (GUID for SP @repoId param) and RepoKey.
        //
        // ⚠️  Check your RepoUserList entity property names:
        //     x.RepoId  — change to x.Repo_Id if your entity uses that name
        //
        // This is called ONCE per request by SyncRequestEnricher.
        // The result drives the fan-out: one SP call per repo for Role 3.
        public async Task<List<UserRepoAccess>> GetUserRepoGuidsAsync(Guid userId)
        {
            return await (
                from repo in _dbContext.RepositoryMasters
                join repoUser in _dbContext.RepoUsers
                    on repo.RepoKey equals repoUser.RepoKey
                where repoUser.UserId == userId
                select new UserRepoAccess
                {
                    RepoId = (Guid)repo.Repo_Id,   // PK from RepositoryMasters
                    RepoKey = repo.RepoKey   // short key
                }
            ).ToListAsync();
        }
    }
}
