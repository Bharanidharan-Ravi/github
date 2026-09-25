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
    public class PermissionRequestController : ControllerBase
    {
        private readonly IPermissionRequestRepo _repo;

        public PermissionRequestController(IPermissionRequestRepo repo)
        {
            _repo = repo;
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] PostPermissionRequestDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });
            if (dto.PermissionDate == default)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Permission Date is required." });
            if (dto.DurationMinutes <= 0)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Permission duration must be greater than zero." });

            var response = await _repo.CreatePermissionRequestAsync(dto);
            return Ok(ApiResponseHelper.Success(response, "Permission request submitted successfully."));
        }

        // PATCH /api/PermissionRequest/{id}/status
        // Body: { Status: "APPROVED" | "REJECTED", RejectReason? } — admin only.
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] PostPermissionRequestStatusDto dto)
        {
            if (dto == null || (dto.Status != "APPROVED" && dto.Status != "REJECTED"))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Status must be APPROVED or REJECTED." });
            if (dto.Status == "REJECTED" && string.IsNullOrWhiteSpace(dto.RejectReason))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "A reason is required when rejecting." });

            var response = await _repo.UpdateStatusAsync(id, dto);
            var message = dto.Status == "APPROVED" ? "Permission request approved." : "Permission request rejected.";
            return Ok(ApiResponseHelper.Success(response, message));
        }

        // PATCH /api/PermissionRequest/{id}/actual-duration
        // Body: { ActualDurationMinutes } — admin only, only on an APPROVED request.
        [HttpPatch("{id:guid}/actual-duration")]
        public async Task<IActionResult> UpdateActualDuration(Guid id, [FromBody] PostPermissionActualDurationDto dto)
        {
            if (dto == null || dto.ActualDurationMinutes <= 0)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Actual duration must be greater than zero." });

            var response = await _repo.UpdateActualDurationAsync(id, dto);
            return Ok(ApiResponseHelper.Success(response, "Actual duration recorded."));
        }
    }
}
