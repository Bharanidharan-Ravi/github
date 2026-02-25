using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using System;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IProjectService
    {
        Task SaveProjectWithAttachmentsAsync(ProjectMaster project, List<AttachmentMaster> attachments);
    }
}
