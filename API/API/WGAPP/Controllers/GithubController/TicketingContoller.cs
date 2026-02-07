using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WGAPP.BusinessLayer.Helpers;
using WGAPP.BusinessLayer.Hub;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.ModelLayer;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.Controllers.GithubController
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/tickets/[controller]")]
    public class TicketingContoller : ControllerBase
    {
        private readonly ITicketingRepository _ticketingRepository;
        private readonly INotificationService _notificationService;
        public TicketingContoller(ITicketingRepository ticketingRepository, INotificationService notificationService)
        {
            _ticketingRepository = ticketingRepository;
            _notificationService = notificationService;
        }
        [HttpPost("CreateIssue")]
        public async Task<IActionResult> CreateIssue([FromBody] IssueMasterDto dto)
        {
            var issue = await _ticketingRepository.AddIssueAsync(dto);
            //var link = new TicketHubModal
            //{
            //    Id = issue.Issue_Id,
            //    Title = issue.Description
            //};

            //await _notificationService.NotifyTicketUpdated(link);

            return Ok(ApiResponseHelper.Success(issue, "Ticket created successfully."));
        } 
        [HttpPost("CreateThreads")]
        public async Task<IActionResult> AddIssueThreadAsync(IssueThreadDTO data)
        {
            var issue = await _ticketingRepository.AddIssueThreadAsync(data);
            return Ok(ApiResponseHelper.Success(issue, "Ticket commented successfully."));
        }

        [HttpPut("updateIssue")]
        public async Task<IActionResult> UpdateIssue(Guid repoId, Guid issueId, [FromBody] IssueMasterDto dto)
        {
            try
            {
                var updated = await _ticketingRepository.UpdateIssueAsync(repoId, issueId, dto);
                return Ok(ApplicationConstants.success_message);
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
        [HttpGet("GetAllIssueData")]
        public async Task<IActionResult> GetAllIssueData(Guid? clientId = null, string FilterBy = null)
        {
            var Issues = await _ticketingRepository.GetAllIssueData(clientId, FilterBy);
            return Ok(Issues);
        }
        [HttpGet("GetIssuesbyId")]
        public async Task<IActionResult> GetIssuesById(Guid? issueId = null, Guid? ProjectId = null)
        {
            var Issues = await _ticketingRepository.GetIssuesById(issueId, ProjectId);
            return Ok(Issues);
        }

         [HttpGet("GetMasterIssueData")]
        public async Task<IActionResult> GetMasterIssueData()
        {
            var Issues = await _ticketingRepository.GetMasterIssueData();
            return Ok(Issues);
        }
        [HttpPost("UploadFilesToTempAsync")]
        public async Task<IActionResult> UploadFilesToTempAsync(IFormFile files)
        {
            var Issues = await _ticketingRepository.UploadFilesToTempAsync(files);
            //return Ok(Issues);
            var reponse = ApiResponseHelper.Success(Issues, "");
            return Ok(reponse);
        }

        [HttpPost("CleanupTempFiles")]
        public async Task<IActionResult> CleanupTempFiles([FromBody] TempReturn files)
        {
            await _ticketingRepository.CleanupTempFiles(files);
            return Ok(new { Code = 200, Message = "Temp files cleaned" });
        }
    }
}
