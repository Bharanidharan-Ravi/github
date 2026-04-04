using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;

namespace APIGateWay.BusinessLayer.Repository
{
    public class AttachmentRepo : IAttachmentRepo
    {
        private readonly IAttachmentService _attachmentService;
        public AttachmentRepo(IAttachmentService attachmentService)
        {
            _attachmentService = attachmentService;
        }

        public async Task<Tempdata> UploadFilesToTempAsync(IFormFile files)
        {
            var res = await _attachmentService.UploadFilesToTempAsync(files);
            return res;
        }
        public  Task CleanupTempFiles(TempReturn filePaths)
        {
            var res =  _attachmentService.CleanupTempFiles(filePaths);
            return res;
        }

        public async Task Upload(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            var attachment = new DBAttachment
            {
                // We REMOVED the ID generation here. 
                // SQL Server will create the integer ID automatically.
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileData = memoryStream.ToArray()
            };
            await _attachmentService.Upload(attachment);
            return;
        }
        public async Task<DBAttachment> GetAttachmentAsync(int id)
        {
            // Simply pass the request down to the service
            return await _attachmentService.GetAttachmentAsync(id);
        }
    }
}
