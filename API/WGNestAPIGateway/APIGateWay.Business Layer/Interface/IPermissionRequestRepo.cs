using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IPermissionRequestRepo
    {
        Task<GetPermissionRequest> CreatePermissionRequestAsync(PostPermissionRequestDto dto);
        Task<GetPermissionRequest> UpdateStatusAsync(Guid id, PostPermissionRequestStatusDto dto);
        Task<GetPermissionRequest> UpdateActualDurationAsync(Guid id, PostPermissionActualDurationDto dto);
        Task<GetPermissionRequest> CancelAsync(Guid id);
    }
}
