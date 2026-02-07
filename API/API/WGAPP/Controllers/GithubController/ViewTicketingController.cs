using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.BusinessLayer.Repository.GithubRepository;
using WGAPP.ModelLayer.GithubModal.TicketingModal;
using WGAPP.ModelLayer;
using WGAPP.ModelLayer.GithubModal.ViewIssues;
using WGAPP.BusinessLayer.Helpers;

namespace WGAPP.Controllers.GithubController
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/tickets/[controller]")]
    public class ViewTicketingController : ControllerBase
    {
        private readonly IViewTicketRepository _viewTicketingRepository;

        public ViewTicketingController(IViewTicketRepository viewTicketRepository)
        {
            _viewTicketingRepository = viewTicketRepository;
        }

        [HttpGet("GetThreadData")]
        public async Task<IActionResult> GetThreadData(Guid IssuesId)
        {

            var updated = await _viewTicketingRepository.GetThreadData(IssuesId);
            return Ok(ApiResponseHelper.Success(updated));

        }

        //[HttpPost("InsertAttachment")]
        //public async Task<IActionResult> InsertAttachmentFile([FromBody] AttachmentDTo dto)
        //{

        //    var updated = await _viewTicketingRepository.InsertAttachment(dto);
        //    return CreatedAtAction(nameof(InsertAttachmentFile), new { id = dto.LineId });

        //}
        //[HttpGet("GetAllIssuesData")]
        //public ActionResult<Tuple<List<GetIssuesModal>, List<LabelMaster>, List<ProjectMaster>>> GetAllIssuesData()
        //{
        //    var result = _viewTicketingRepository.GetAllIssuesData();
        //    return Ok(result);
        //}
    }
}
