using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingSchedulerControler : ControllerBase
    {
        private readonly IMeetingSchedulerRepo _meetingRepo;
        public MeetingSchedulerControler(IMeetingSchedulerRepo meetingRepo)
        {
            _meetingRepo = meetingRepo;
        }
        [HttpPost("CreateMeeting")]
        public async Task<IActionResult> CreateMeeting([FromBody] PostMeetingDto meetingDto)
        {

            var response = await _meetingRepo.CreateMeetingAsync(meetingDto);
            return Ok(ApiResponseHelper.Success(response, "Meeting Scheduled successfully."));
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMeetingAsync(Guid id, [FromBody] PutMeetingDto meetingDto)
        {
            if (meetingDto == null || id != meetingDto.Meeting_Id)
            {
                return BadRequest(new { message = "Invalid meeting data or ID mismatch." });
            }

            var response = await _meetingRepo.UpdateMeetingAsync(meetingDto);
            return Ok(ApiResponseHelper.Success(response, "Meeting Scheduled successfully."));
        }
        [HttpPost("CompleteMeeting")]
        public async Task<IActionResult> CompleteMeeting([FromBody] MeetingCompletionDto dto)
        {
            Guid userId = GetCurrentUserId();

            await _meetingRepo.CompleteMeetingAsync(dto, userId);

            return Ok(new
            {
                Success = true,
                Message = "Meeting completed successfully."
            });
        }
        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirst("UserId")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("User is not authenticated.");

            return Guid.Parse(userId);
        }
    }
}