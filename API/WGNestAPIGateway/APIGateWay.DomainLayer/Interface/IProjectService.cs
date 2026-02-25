using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.MasterData.APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IProjectService
    {
        Task SaveProjectWithAttachmentsAsync(ProjectMaster project, List<AttachmentMaster> attachments);
    }
}
