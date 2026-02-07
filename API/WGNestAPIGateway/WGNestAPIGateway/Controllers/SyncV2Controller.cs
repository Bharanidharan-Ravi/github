using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncV2Controller : ControllerBase
    {
        private readonly ISyncRepositoryV2 _syncRepository;

        public SyncV2Controller(ISyncRepositoryV2 syncRepository)
        {
            _syncRepository = syncRepository;
        }

        [HttpPost("v2")]
        public async Task<IActionResult> SyncDynamicV2(
            [FromBody] DynamicSyncRequest request)
        {
            // -------- Validation --------
            if (request == null || request.ConfigKeys == null || !request.ConfigKeys.Any())
            {
                return BadRequest(new
                {
                    c = "VALIDATION_ERROR",
                    m = "ConfigKeys are required"
                });
            }

            // -------- IMPORTANT --------
            // Skip global response wrapping middleware
            HttpContext.Items["SkipResponseWrap"] = true;

            // -------- Execute Sync --------
            var response = await _syncRepository.ExecuteAsync(request);

            // -------- HTTP Status Handling --------
            bool anySuccess = response.Res.Any(r => r.Value.Ok);
            bool anyFailure = response.Res.Any(r => !r.Value.Ok);

            if (anySuccess && anyFailure)
                return StatusCode(207, response);

            if (!anySuccess)
                return StatusCode(500, response);

            return Ok(response);
        }
    }
}
