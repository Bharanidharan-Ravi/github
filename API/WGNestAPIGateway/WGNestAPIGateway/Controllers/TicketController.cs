using APIGateWay.Business_Layer.Interface;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketController : ControllerBase
    {
        private readonly ITicketRepo _ticketRepo;

        public TicketController(ITicketRepo ticketRepo)
        {
            _ticketRepo = ticketRepo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/ticket
        // All roles. RepoScopeHandler validates RepoId from body.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromBody] PostTicketDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (!dto.RepoId.HasValue)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "RepoId is required." });

            var result = await _ticketRepo.CreateTicketAsync(dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/ticket/{id}
        // All roles — any role can update a ticket in their repo.
        // Scope: RepoId in body → validated directly.
        //        RepoId absent  → handler looked up ticket's Repo_Id by {id}.
        // Labels fully replaced when labelId is sent in body.
        // No sequence call — Issue_Code never changes on update.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateTicketDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Title is required." });

            var result = await _ticketRepo.UpdateTicketAsync(id, dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/ticket/{id}/status
        // All roles — any role can change status of a ticket in their repo.
        // Body: { "Status": 2 }   (no RepoId required)
        //
        // Scope flow:
        //   RepoScopeHandler sees PATCH + no Repo_Id in body
        //   → calls GetTicketRepoIdAsync(id) to look up ticket's Repo_Id from DB
        //   → validates against user's allowed repos
        //   → fails → 403 before controller ever runs
        //
        // Only Status column updated. Labels untouched.
        // EF sends: UPDATE IssueMasters SET Status=@s, UpdatedAt=@t, UpdatedBy=@u
        //           WHERE Issue_Id=@id
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateTicketStatus(Guid id, [FromBody] UpdateTicketStatusDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Status is required." });

            var result = await _ticketRepo.UpdateTicketStatusAsync(id, dto);
            return Ok(result);
        }
    }
}
