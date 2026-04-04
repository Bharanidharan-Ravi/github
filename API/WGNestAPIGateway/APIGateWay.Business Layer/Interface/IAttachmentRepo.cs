using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;
using System;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IAttachmentRepo
    {
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
        Task CleanupTempFiles(TempReturn filePaths);
        Task Upload(IFormFile file);
        Task<DBAttachment> GetAttachmentAsync(int id);

    }
}
