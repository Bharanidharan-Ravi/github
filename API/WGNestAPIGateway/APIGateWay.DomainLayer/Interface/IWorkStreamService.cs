using APIGateWay.ModalLayer.MasterData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Interface
{
    public interface IWorkStreamService
    {
        // ── Main entry point ─────────────────────────────────────────────────
        // Called from Thread / Ticket create / Ticket update.
        // Must be called INSIDE an existing ExecuteInTransactionAsync block.
        //
        // What it does:
        //   1. Resolve StreamName from department if not supplied
        //   2. Resolve ResourceId based on StreamName rule
        //   3. Close any previous active stream for this user on this ticket
        //      if they are changing to a different StreamName
        //   4. Upsert: last row same StreamName → update %, else insert new row
        //
        // Returns WorkStreamResult so caller can update TicketMaster.StreamId etc.
        Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx);
    }
}
