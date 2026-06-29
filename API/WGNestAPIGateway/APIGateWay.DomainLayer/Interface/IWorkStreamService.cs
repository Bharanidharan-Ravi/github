using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IWorkStreamService
    {
        Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto);
        Task<int?> GetDepartmentNameAsync(Guid? resourceId);

        /// <summary>
        /// DELETE THIS LATER
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx);
        Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx);

        Task ClearWorkStreamsAsync(Guid issueId);
        Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds);
        Task<long> PostMeetingScheduledAsync(MeetingMaster meeting, List<MeetingAttendance> attendance, Guid createdBy);
        Task UpdateMeetingThreadAsync(MeetingMaster meeting, List<MeetingAttendance> attendance, Guid updatedBy);
        Task UpdateMeetingCompletionThreadAsync(MeetingMaster meeting, MeetingCompletionDto dto, Guid completedBy);
    }
}





//// Clear all — TicketRepo when ResourceIds = [] on update
