using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Interface
{
    public interface IMeetingSchedulerRepo
    {
        Task<GetMeetingDto> CreateMeetingAsync(PostMeetingDto meetingDto);
        Task<GetMeetingDto> UpdateMeetingAsync(PutMeetingDto meetingDto);
    }
}
