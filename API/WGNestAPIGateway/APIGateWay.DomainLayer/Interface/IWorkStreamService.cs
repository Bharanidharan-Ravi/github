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
        Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx);

        // Bulk upsert — TicketRepo (multiple assignees on create/update)
        Task<List<WorkStreamResult>> UpsertWorkStreamsAsync(
            Guid? issueId,
            List<Guid> resourceIds,
            int? streamStatus,
            decimal? completionPct,
            DateTime? targetDate);

        Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds);
        // Clear all — TicketRepo when ResourceIds = [] on update
        Task ClearWorkStreamsAsync(Guid issueId);
        Task<string> GetDepartmentNameAsync(Guid? resourceId);

        //Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid issueId);
    }
}
