using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.BusinessLayer.Interface.GithubInterface
{
    public interface ITicketingRepository
    {
        Task<GetAllIssueData> AddIssueAsync(IssueMasterDto issueMasterDto);
        Task<ThreadbyTicketId> AddIssueThreadAsync(IssueThreadDTO data);
        Task<IssueMaster> UpdateIssueAsync(Guid repoId, Guid issueId, IssueMasterDto dto);
        Task<List<GetAllIssueData>> GetAllIssueData(Guid? clientId = null, string FilterBy = null);
        Task<List<GetAllIssueData>> GetMasterIssueData();
        Task<List<GetAllIssueData>> GetIssuesById(Guid? issueId = null, Guid? ProjectId = null);
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
        //Task<List<GetLabelMaster>> GetLabelMasters();
        Task CleanupTempFiles(TempReturn filePaths);
    }
}
