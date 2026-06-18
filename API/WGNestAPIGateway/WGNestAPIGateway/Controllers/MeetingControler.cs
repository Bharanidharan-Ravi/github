using APIGateWay.Business_Layer.Interface;
using Microsoft.AspNetCore.Mvc;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static APIGateWay.Business_Layer.SignalRHub.RealtimeEntities;


namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingControler:ControllerBase
    {
        private readonly IMeetingRepo _meetingRepo;
        public MeetingControler(IMeetingRepo meetingRepo)
        {
            _meetingRepo = meetingRepo;
        }
        [HttpPost("CreateMeeting")]
        public async Task<IActionResult> CreateMeeting([FromBody] PostingmeetingDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Requested body is required." });
            if (string.IsNullOrWhiteSpace(dto.title))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Title is required." });
            var response = await _meetingRepo.CreateMeetingAsync(dto);
            return Ok(ApiResponseHelper.Success(response, "Meeting Scheduled successfully."));
        }

    }
}
