using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.BusinessLayer.Hub;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.BusinessLayer.Repository.GithubRepository
{
    public class TicketingRepository : ITicketingRepository
    {
        private readonly ITicketingService _TicketingService;
        private readonly INotificationService _notificationService;
        private readonly ILoginContextService _loginContextService;

        public TicketingRepository(ITicketingService TicketingService, INotificationService notificationService, ILoginContextService loginContextService)
        {
            _TicketingService = TicketingService;
            _notificationService = notificationService;
            _loginContextService = loginContextService;
        }

        public async Task<GetAllIssueData> AddIssueAsync(IssueMasterDto issueMasterDto)
        {
            var issue = new IssueMaster
            {
                Repo_Id = issueMasterDto.Repo_Id,
                Title = issueMasterDto.Title,
                Description = issueMasterDto.Description,
                Issuer_Id = issueMasterDto.Issuer_Id,
                Created_On = DateTime.UtcNow,
                Updated_On = issueMasterDto.Updated_On,
                Project_Id = issueMasterDto.Project_Id,
                Assignee_Id = issueMasterDto.Assignee_Id,
                Due_Date = issueMasterDto.Due_Date,
                Status = issueMasterDto.Status,
                Issuelink_Id = issueMasterDto.Issuelink_Id,
                Issue_Code = issueMasterDto.Issue_Code,
            };
            // ✅ Now you can pass both
            var link = new TicketHubModal
            {
                Id = issue.Issue_Id,
                Title = issue.Description
            };
            var response = await _TicketingService.AddIssuesAsync(issue, issueMasterDto.Labels, issueMasterDto.TempReturns);
            //var response = await _TicketingService.AddIssuesAsync(issue);
            int role =int.Parse(_loginContextService.Role);
            await _notificationService.TicketCreated(response, role);
            return response;
        }

        public async Task<ThreadbyTicketId> AddIssueThreadAsync(IssueThreadDTO data)
        {
            var response = await _TicketingService.AddIssueThreadAsync(data.thread, data.TempReturns);
            return response;
        }

        public async Task<IssueMaster> UpdateIssueAsync(Guid repoId, Guid issueId, IssueMasterDto dto)
        {
            var existing = await _TicketingService.GetIssueByRepoAndIssueIdAsync(repoId, issueId);

            if (existing == null)
                throw new Exception($"Issue not found for Repo_Id: {repoId}, Issue_Id: {issueId}");

            // Map updated fields
            existing.Title = dto.Title;
            existing.Description = dto.Description;
            existing.Issuer_Id = dto.Issuer_Id;
            existing.Created_On = dto.Created_On;
            existing.Updated_On = dto.Updated_On;
            existing.Project_Id = dto.Project_Id;
            //existing.Label_Id = dto.Label_Id;
            existing.Assignee_Id = dto.Assignee_Id;
            existing.Due_Date = dto.Due_Date;
            existing.Status = dto.Status;
            //existing.Attechement_Id = dto.Attechement_Id;
            existing.Issuelink_Id = dto.Issuelink_Id;

            return await _TicketingService.UpdateIssueAsync(existing);
        }

        public async Task<List<GetAllIssueData>> GetAllIssueData(Guid? clientId = null, string FilterBy = null)
        {
            var issues = await _TicketingService.GetAllIssueData(clientId, FilterBy);
            return issues;
        } 
        public async Task<List<GetAllIssueData>> GetIssuesById(Guid? issueId = null, Guid? ProjectId = null)
        {
            var issues = await _TicketingService.GetIssuesById(issueId, ProjectId);
            return issues;
        }
        public async Task<List<GetAllIssueData>> GetMasterIssueData()
        {
            var issues = await _TicketingService.GetMasterIssueData();
            return issues;
        }
        public async Task<Tempdata> UploadFilesToTempAsync(IFormFile files)
        {
            var temp = await _TicketingService.UploadFilesToTempAsync(files);
            return temp;
        }


        public Task CleanupTempFiles(TempReturn filePaths)
        {
            var LabelMaster =  _TicketingService.CleanupTempFiles(filePaths);
            return LabelMaster;
        }

    }
}
