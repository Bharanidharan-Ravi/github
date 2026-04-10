using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModalLayer.PostData;
using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

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
            return Ok(ApiResponseHelper.Success(res, "File added successfully!"));
        }
        [HttpPost("tempCleanUp")]
        public async Task<IActionResult> CleanupTempFiles(TempReturn filePaths)
        {
            var res = _attachmentRepo.CleanupTempFiles(filePaths);
            return Ok(ApiResponseHelper.Success(res, "File removed successfully!"));
        }

        [HttpPost("upload")]
        [AllowAnonymous]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            // 1. Await the method, but don't assign it to a variable since it returns nothing
            await _attachmentRepo.Upload(file);

            // 2. Return a simple success message manually
            return Ok("File uploaded successfully!");
        }
        [HttpGet("download/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Download(int id)
        {
            var attachment = await _attachmentRepo.GetAttachmentAsync(id);

            if (attachment == null)
                return NotFound("File not found");

            // Convert the binary byte array into a Base64 string
            var base64String = Convert.ToBase64String(attachment.FileData);

            // Return the data as a normal object. 
            // Your global wrapper will intercept this and put it inside "Message"
            var fileDataResponse = new
            {
                FileName = attachment.FileName,
                ContentType = attachment.ContentType ?? "application/octet-stream",
                FileData = base64String
            };

            return Ok(fileDataResponse);
        }
    }
}
