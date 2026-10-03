using APIGateWay.ModalLayer.nugerModalV2;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    public static class SyncResponseResult
    {
        /// <summary>
        /// Returns a sync-style response ({ V, Rid, St, Res }) unwrapped:
        /// 200 all keys ok, 207 some failed, 500 all failed.
        /// </summary>
        public static IActionResult SyncResult(this ControllerBase controller, SyncResponseV2 response)
        {
            // Skip global response wrapping middleware
            controller.HttpContext.Items["SkipResponseWrap"] = true;

            bool anySuccess = response.Res.Any(r => r.Value.Ok);
            bool anyFailure = response.Res.Any(r => !r.Value.Ok);

            if (anySuccess && anyFailure)
                return controller.StatusCode(207, response);

            if (!anySuccess)
                return controller.StatusCode(500, response);

            return controller.Ok(response);
        }
    }
}
