using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TicketFeedbackController : ControllerBase
    {
        private readonly ITicketFeedbackRepo _feedbackRepo;

        public TicketFeedbackController(ITicketFeedbackRepo feedbackRepo)
        {
            _feedbackRepo = feedbackRepo;
        }

        [HttpPost("FeedbackPost")]
        public async Task<IActionResult> PostFeedback([FromBody] PostTicketFeedbackDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5 || dto.UserIds == null || !dto.UserIds.Any())
            {
                return BadRequest(ApiResponseHelper.Failure("A valid rating (1-5) and at least one user are required."));
            }
            var result = await _feedbackRepo.SubmitFeedbackAsync(dto);
            return Ok(ApiResponseHelper.Success(result, "Feedback Submitted successfully"));
        }

        [HttpGet("ticket/{ticketId:guid}")]
        public async Task<IActionResult> GetFeedbacksByTicket([FromRoute] Guid ticketId)
        {
            var data = await _feedbackRepo.GetFeedbacksByTicketAsync(ticketId);
            return Ok(ApiResponseHelper.Success(data));
        }
    }
}
