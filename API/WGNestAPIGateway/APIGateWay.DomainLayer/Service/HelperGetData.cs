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
    public class HelperGetData :IHelperGetData
    {
        private readonly APIGatewayDBContext _dbContext;
        public HelperGetData(APIGatewayDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> GetRepoKeyByIdAsync(Guid repoId)
        {
            var repoKey = await _dbContext.RepositoryMasters
                .Where(x => x.Repo_Id == repoId)
                .Select(x => x.RepoKey)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(repoKey))
            {
                throw new Exception("Repository not found or invalid.");
            }

            return repoKey;
        }
    }
}
