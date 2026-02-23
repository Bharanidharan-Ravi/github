using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IAttachmentRepo
    {
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
        Task CleanupTempFiles(TempReturn filePaths);

    }
}
