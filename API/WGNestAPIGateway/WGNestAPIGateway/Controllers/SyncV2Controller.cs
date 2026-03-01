using APIGateWay.BusinessLayer.Auth;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer.nugerModalV2;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncV2Controller : ControllerBase
    {
        private readonly ISyncRepositoryV2 _syncRepo;
        private readonly ISyncRequestEnricher _enricher;

        public SyncV2Controller(
            ISyncRepositoryV2 syncRepo,
            ISyncRequestEnricher enricher)
        {
            _syncRepo = syncRepo;
            _enricher = enricher;
        }

        /// <summary>
        /// POST /sync/v2
        ///
        /// Flow:
        ///   1. SyncRoleGuard validates each config key against the user's role
        ///   2. Denied keys → immediate error result in response (no DB call)
        ///   3. Allowed keys → pass to SyncRepositoryV2 for execution
        ///   4. Merge denied + allowed results into one response
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
            // ── Step 1: Enrich the request ─────────────────────────────────────
            // For Role 3: auto-injects repoIds, blocks disallowed keys, fans out
            // For Role 1/2: passes through unchanged, zero overhead
            var enriched = await _enricher.EnrichAsync(request);

            // ── Step 2: Build response skeleton ───────────────────────────────
            var response = new SyncResponseV2
            {
                Rid = Guid.NewGuid().ToString(),
                St = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Pre-fill denied keys (no DB call made for these)
            foreach (var (key, reason) in enriched.DeniedKeys)
            {
                response.Res[key] = new SyncResultV2
                {
                    Ok = false,
                    Err = new SyncErrorV2 { C = "ACCESS_DENIED", M = reason, R = false }
                };
            }

            // ── Step 3: Execute allowed units ──────────────────────────────────
            if (enriched.Units.Any())
            {
                var repoResponse = await _syncRepo.ExecuteUnitsAsync(enriched.Units);

                foreach (var kv in repoResponse.Res)
                    response.Res[kv.Key] = kv.Value;
            }
            // Skip global response wrapping middleware
            HttpContext.Items["SkipResponseWrap"] = true;

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
