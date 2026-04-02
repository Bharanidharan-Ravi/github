using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/dailyplan")]
    public class DailyPlanController : ControllerBase
    {
        private readonly IDailyPlanRepo _planRepo;
        public DailyPlanController(IDailyPlanRepo planRepo) => _planRepo = planRepo;

        // GET /api/dailyplan?date=2024-01-15
        // date optional — defaults to today UTC
        [HttpGet]
        public async Task<IActionResult> GetTodayPlan([FromQuery] DateTime? date)
        {
            var target = date?.Date ?? DateTime.UtcNow.Date;
            var plans = await _planRepo.GetTodayPlanAsync(target);
            return Ok(plans);
        }

        // POST /api/dailyplan
        // Body: { TicketId, ProjectId? }
        // Idempotent — safe to call multiple times for same ticket today
        [HttpPost]
        public async Task<IActionResult> CheckTicket([FromBody] List<CreateDailyPlanDto> dto)
        {
            if (dto == null || dto.Count == 0)
            {
                return BadRequest(new { code = "VALIDATION_ERROR", ErrorMessage = "At least one ticket is required" });
            }
            if (dto.Any(dto => dto.TicketId == Guid.Empty))
            {
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "TicketId is required." });
            }

            var result = await _planRepo.CheckTicketAsync(dto);
            return Ok(result);
        }

        // PATCH /api/dailyplan/{id}/uncheck
        // Body: { UncheckComment: "reason..." }
        // Returns 400 with code PLAN_LOCKED if ticket was marked Success
        [HttpPatch("{id:int}/uncheck")]
        public async Task<IActionResult> UncheckTicket(int id, [FromBody] UncheckPlanDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.UncheckComment))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "A comment is required when unchecking." });

            try
            {
                var result = await _planRepo.UncheckTicketAsync(id, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Code = "PLAN_LOCKED", ErrorMessage = ex.Message });
            }
        }
    }
}
