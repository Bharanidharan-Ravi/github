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
        public async Task<List<string>> GetUserRepoIdsAsync(Guid userId)
        {
            return await _dbContext.RepoUsers
                .Where(x => x.UserId == userId)
                .Select(x => x.RepoKey)
                .ToListAsync();
        }
    }
}
