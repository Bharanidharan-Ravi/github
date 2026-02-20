using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IAttachmentService
    {
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
    }
}
