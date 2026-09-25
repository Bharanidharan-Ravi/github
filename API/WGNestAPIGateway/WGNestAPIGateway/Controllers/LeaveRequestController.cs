using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace APIGateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestController : ControllerBase
    {
        private readonly ILeaveRequestRepo _repo;

        public LeaveRequestController(ILeaveRequestRepo repo)
        {
            _repo = repo;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] PostLeaveRequestDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });
            if (dto.LeaveFrom == default || dto.LeaveTo == default)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "From Date and To Date are required." });
            if (dto.LeaveTo.Date < dto.LeaveFrom.Date)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "To Date cannot be before From Date." });
            if (dto.LeaveTypeId <= 0)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Leave Type is required." });

            var response = await _repo.CreateLeaveRequestAsync(dto);
            return Ok(ApiResponseHelper.Success(response, "Leave request submitted successfully."));
        }

        // PATCH /api/LeaveRequest/{id}/status
        // Body: { Status: "APPROVED" | "REJECTED", RejectReason? } — admin only.
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] PostLeaveRequestStatusDto dto)
        {
            if (dto == null || (dto.Status != "APPROVED" && dto.Status != "REJECTED"))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Status must be APPROVED or REJECTED." });
            if (dto.Status == "REJECTED" && string.IsNullOrWhiteSpace(dto.RejectReason))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "A reason is required when rejecting." });

            var response = await _repo.UpdateStatusAsync(id, dto);
            var message = dto.Status == "APPROVED" ? "Leave request approved." : "Leave request rejected.";
            return Ok(ApiResponseHelper.Success(response, message));
        }
    }
}
