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
        Task<string> GetDepartmentNameAsync(Guid? resourceId);

        /// <summary>
        /// DELETE THIS LATER
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx);
        Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx);

        Task ClearWorkStreamsAsync(Guid issueId);
        Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds);
    }
}





//// Clear all — TicketRepo when ResourceIds = [] on update
