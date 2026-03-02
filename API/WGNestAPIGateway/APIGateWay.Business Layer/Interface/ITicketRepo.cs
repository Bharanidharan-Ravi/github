using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface ITicketRepo
    {
        // ── EXISTING ──────────────────────────────────────────────────────────
        Task<GetTickets> CreateTicketAsync(PostTicketDto ticketDto);

        // ── NEW ───────────────────────────────────────────────────────────────

        // Full update — title, description, attachments, labels, priority etc.
        // PUT /api/ticket/{id}
        // No sequence call — Issue_Code never regenerates on update.
        // Labels: full replace — old ones deleted, new list inserted.
        Task<GetTickets> UpdateTicketAsync(Guid ticketId, UpdateTicketDto dto);

        // Status-only update — changes ONLY the Status column.
        // PATCH /api/ticket/{id}/status    Body: { "Status": 2 }
        // RepoScopeHandler looked up ticket's Repo_Id from DB before this runs.
        Task<GetTickets> UpdateTicketStatusAsync(Guid ticketId, UpdateTicketStatusDto dto);
    }
}
