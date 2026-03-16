using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkStreamController : ControllerBase
    {
        private readonly IWorkStreamRepo _workStreamRepo;
        public WorkStreamController(IWorkStreamRepo workStreamRepo)
        {
            _workStreamRepo = workStreamRepo;
        }

        [HttpPost]
        public async Task<IActionResult> PostWorkStream([FromBody] PostWorkStreamDto dto)
        {
            // ── Validation ────────────────────────────────────────────────────
            if (dto.IssueId == Guid.Empty)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "IssueId is required." });

            if (string.IsNullOrWhiteSpace(dto.StreamName))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "StreamName is required." });

            if (!dto.UseLastThread && string.IsNullOrWhiteSpace(dto.Comment))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Comment is required when not using last thread." });

            try
            {
                var result = await _workStreamRepo.PostWorkStreamAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // e.g. UseLastThread=true but no thread exists yet
                return BadRequest(new { Code = "BUSINESS_RULE", ErrorMessage = ex.Message });
            }
        }
    }
}
