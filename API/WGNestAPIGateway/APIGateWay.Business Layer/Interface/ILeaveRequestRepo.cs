using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface ILeaveRequestRepo
    {
        Task<GetLeaveRequest> CreateLeaveRequestAsync(PostLeaveRequestDto dto);
        Task<GetLeaveRequest> UpdateStatusAsync(Guid id, PostLeaveRequestStatusDto dto);
    }
}
