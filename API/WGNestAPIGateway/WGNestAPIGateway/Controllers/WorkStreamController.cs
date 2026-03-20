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
                return BadRequest(new
                {
                    Code = "VALIDATION_ERROR",
                    ErrorMessage = "IssueId is required."
                });

            //if (string.IsNullOrWhiteSpace(dto.StreamName))
            //    return BadRequest(new
            //    {
            //        Code = "VALIDATION_ERROR",
            //        ErrorMessage = "StreamName is required."
            //    });

            // ── Never allow both at the same time ─────────────────────────────────
            if (dto.ReportTestFailure && dto.ClearTestFailure)
                return BadRequest(new
                {
                    Code = "VALIDATION_ERROR",
                    ErrorMessage = "ReportTestFailure and ClearTestFailure cannot both be true. Send one at a time."
                });

            // ── Failure comment is mandatory when reporting ────────────────────────
            if (dto.ReportTestFailure && string.IsNullOrWhiteSpace(dto.TestFailureComment))
                return BadRequest(new
                {
                    Code = "VALIDATION_ERROR",
                    ErrorMessage = "TestFailureComment is required when ReportTestFailure is true."
                });

            // ── PercentageDrop must be meaningful (min 1, max 100) ────────────────
            if (dto.ReportTestFailure && dto.PercentageDrop.HasValue && dto.PercentageDrop <= 0)
                return BadRequest(new
                {
                    Code = "VALIDATION_ERROR",
                    ErrorMessage = "PercentageDrop must be greater than 0 when reporting a test failure. Default is 30 if not provided."
                });

            // ── Comment required unless UseLastThread=true ────────────────────────
            //if (dto.UseLastThread != true && string.IsNullOrWhiteSpace(dto.Comment)
            //    && !dto.ReportTestFailure && !dto.ClearTestFailure)
            //    return BadRequest(new
            //    {
            //        Code = "VALIDATION_ERROR",
            //    });


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
