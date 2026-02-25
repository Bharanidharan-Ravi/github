using System;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Http;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IAttachmentService
    {
        Task<Tempdata> UploadFilesToTempAsync(IFormFile files);
        Task<ProcessedAttachmentResult> ProcessAndCopyAttachmentsAsync(string rawHtml, List<Tempdata> temps, string relativePermPath, string? entityId, string module);
        Task CleanupTempFiles(TempReturn filePaths);
        void RollbackPhysicalFiles(List<string> filePaths);
    }
}
