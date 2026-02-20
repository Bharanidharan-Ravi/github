using APIGateWay.Business_Layer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
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
    }
}
