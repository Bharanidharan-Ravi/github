using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IHelperGetData
    {
        Task<ProjectKeysDto> GetProjectByIdAsync(Guid projId);
        Task<string> GetRepoKeyByIdAsync(Guid repoId);
        Task<IssueRepositoryInfo> GetIssueRepositoryInfoAsync(Guid IssueId);
    }
}
