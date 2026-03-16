using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
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

        public async Task<ProjectKeysDto> GetProjectByIdAsync(Guid projId)
        {
            var result = await _dbContext.ProjectMasters
                .Where(x => x.Id == projId)
                .Select(x => new ProjectKeysDto
                {
                    RepoKey = x.RepoKey,
                    ProjectKey = x.ProjectKey
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                throw new Exception("Project not found.");
            }

            return result;
        }
        public async Task<string> GetRepoKeyByIdAsync(Guid? repoId)
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

        public async Task<IssueRepositoryInfo> GetIssueRepositoryInfoAsync(Guid IssueId)
        {
            var issue = await _dbContext.ISSUEMASTER
                .Where(x => x.Issue_Id == IssueId)
                .Select(x => new { x.RepoId, x.RepoKey, x.Title })
                .FirstOrDefaultAsync();

            return new IssueRepositoryInfo
            {
                RepoId = issue.RepoId,
                RepoKey = issue.RepoKey,
                IssueTitle = issue.Title,
            };

        }
    }
}
