using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncV2Controller : ControllerBase
    {
        private readonly ISyncRepositoryV2 _syncRepo;

        public SyncV2Controller(ISyncRepositoryV2 syncRepo)
        {
            _syncRepo = syncRepo;
        }

        /// <summary>
        /// POST /sync/v2
        ///
        /// Flow (SyncRepositoryV2.RunAsync):
        ///   1. SyncRequestEnricher validates each config key against the user's role
        ///   2. Denied keys → immediate error result in response (no DB call)
        ///   3. Allowed keys → executed in parallel
        ///   4. Denied + allowed results merged into one response
        /// </summary>
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

            return this.SyncResult(await _syncRepo.RunAsync(request));
        }
    }
}
