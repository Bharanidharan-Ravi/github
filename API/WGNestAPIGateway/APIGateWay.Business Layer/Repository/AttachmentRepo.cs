using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
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
    }
}
