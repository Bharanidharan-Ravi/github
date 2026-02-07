using Azure;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.MasterData;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface ITicketingService
    {
        Task<GetAllIssueData> AddIssuesAsync(IssueMaster issue, List<ISSUE_LABELS> labels, TempReturn temps);

        Task<ThreadbyTicketId> AddIssueThreadAsync(IssuesThread thread, TempReturn temp);
        //Task<GetAllIssueData> AddIssuesAsync(IssueMaster issue);
        Task<IssueMaster> GetIssueByRepoAndIssueIdAsync(Guid repoId, Guid issueId);
        Task<IssueMaster> UpdateIssueAsync(IssueMaster issue);
        Task<List<GetAllIssueData>> GetAllIssueData(Guid? clientId = null, string FilterBy = null);
        Task<List<GetAllIssueData>> GetMasterIssueData();
        Task<List<GetAllIssueData>> GetIssuesById(Guid? issueId = null, Guid? ProjectId = null);
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
        //Task<List<GetLabelMaster>> GetLabelMasters();
        Task CleanupTempFiles(TempReturn filePaths);
    }
}
