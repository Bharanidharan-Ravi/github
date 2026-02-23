using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Azure;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttachmentController : ControllerBase
    {
        private readonly IAttachmentRepo _attachmentRepo;
        public AttachmentController(IAttachmentRepo attachmentRepo)
        {
            _attachmentRepo = attachmentRepo;
        }

        [HttpPost("tempUpload")]
        public async Task<IActionResult>UploadFilesToTempAsync(IFormFile files)
        {
            var res = await _attachmentRepo.UploadFilesToTempAsync(files);
            return Ok(ApiResponseHelper.Success(res, "Repository create successfully."));
        }
        [HttpPost("tempCleanUp")]
        public async Task<IActionResult> CleanupTempFiles(TempReturn filePaths)
        {
            var res = _attachmentRepo.CleanupTempFiles(filePaths);
            return Ok(ApiResponseHelper.Success(res, "Repository create successfully."));
        }
    }
}
