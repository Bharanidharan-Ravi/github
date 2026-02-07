using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.nugetmodal;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly ISyncRepository _syncRepository;

        public SyncController(ISyncRepository syncRepository)
        {
            _syncRepository = syncRepository;
        }

        [HttpPost("dynamic")]
        public async Task<IActionResult> SyncDynamic([FromBody] DynamicSyncRequest request)
        {
            if (request?.ConfigKeys == null || !request.ConfigKeys.Any())
            {
                return BadRequest(new
                {
                    Code = "VALIDATION_ERROR",
                    Message = "ConfigKeys are required"
                });
            }

            HttpContext.Items["SkipResponseWrap"] = true;

            var response = await _syncRepository.ExecuteAsync(request);

            bool anySuccess = response.Results.Any(r => r.Value.Ok);
            bool anyFailure = response.Results.Any(r => !r.Value.Ok);

            if (anySuccess && anyFailure)
                return StatusCode(207, response);

            if (!anySuccess)
                return StatusCode(500, response);

            return Ok(response);
        }
    }
}
