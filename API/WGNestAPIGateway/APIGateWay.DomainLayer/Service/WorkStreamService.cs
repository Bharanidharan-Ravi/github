using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Service
{
    // =========================================================================
    // WorkStreamService
    //
    // WHAT IS A WORKSTREAM ROW?
    //   One row = one person working on one ticket in one department stage.
    //
    //   IssueId      → which ticket
    //   ResourceId   → which assignee (person doing the work)
    //   StreamName   → which department (auto-resolved from EMPLOYEEMASTER.Team
    //                  of the ResourceId — NOT from logged-in user)
    //   StreamStatus → current state of their work:
    //                  1 = In Progress
    //                  2 = Hold
    //                  3 = Awaiting Client
    //                  4 = Completed
    //   CompletionPct → 0 to 100
    //
    // ONE ROW PER (IssueId + ResourceId):
    //   Same person, same ticket → always UPDATE the existing row
    //   New person or new ticket → INSERT a new row
    //
    // CALLED FROM:
    //   ThreadRepo.CreateThreadAsync  → single upsert (one person posting)
    //   TicketRepo.CreateTicketAsync  → bulk upsert  (multiple assignees)
    //   TicketRepo.UpdateTicketAsync  → bulk upsert or clear
    //
    // MUST BE CALLED INSIDE ExecuteInTransactionAsync so WorkStream changes
    // are part of the same transaction as the ticket/thread insert/update.
    // =========================================================================

    public class WorkStreamService : IWorkStreamService
    {
        private readonly IDomainService _domainService;
        private readonly APIGatewayDBContext _db;

        public WorkStreamService(
            IDomainService domainService,
            APIGatewayDBContext db)
        {
            _domainService = domainService;
            _db = db;
        }

        // =====================================================================
        // SINGLE UPSERT
        // Used by: ThreadRepo.CreateThreadAsync (one person per thread)
        // =====================================================================
        public async Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx)
        {
            // Step 1: resolve StreamName from the ASSIGNEE's department
            // Uses ctx.ResourceId — NOT _loginContext.userId
            // e.g. ResourceId = UserA → EMPLOYEEMASTER.Team = "Web Development"
            //      → StreamName = "Web Development"
            var streamName = await GetDepartmentNameAsync(ctx.ResourceId);

            // Step 2: check if this assignee already has a row for this ticket
            // Scoped to (IssueId + ResourceId) — each person has their own row
            // so multiple assignees never conflict with each other
            var existingRow = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == ctx.IssueId &&
                    ws.ResourceId == ctx.ResourceId);

            if (existingRow != null)
            {
                // Row exists → UPDATE status + percentage only
                // StreamName never changes (department doesn't change mid-ticket)
                // TargetDate updated only if a new one was supplied
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == existingRow.StreamId,
                    ws =>
                    {
                        ws.StreamStatus = ctx.StreamStatus;
                        ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

                        if (ctx.TargetDate.HasValue)
                            ws.TargetDate = ctx.TargetDate;

                        // UpdatedAt + UpdatedBy → auto-set by DBContext audit
                    }
                );

                return new WorkStreamResult
                {
                    StreamId = existingRow.StreamId,
                    StreamName = existingRow.StreamName,
                    ResourceId = existingRow.ResourceId!.Value,
                    StreamStatus = ctx.StreamStatus,
                    WasInserted = false,
                };
            }
            else
            {
                // No row yet → INSERT new row for this assignee on this ticket
                var newRow = new WorkStream
                {
                    IssueId = ctx.IssueId,
                    StreamName = streamName,        // from assignee's dept
                    ResourceId = ctx.ResourceId,
                    StreamStatus = ctx.StreamStatus,
                    CompletionPct = ctx.CompletionPct ?? 0,
                    TargetDate = ctx.TargetDate,
                    // CreatedAt, UpdatedAt, CreatedBy, UpdatedBy → DBContext audit
                };

                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                // After SaveChangesAsync → newRow.StreamId is now the DB-generated GUID

                return new WorkStreamResult
                {
                    StreamId = newRow.StreamId,
                    StreamName = newRow.StreamName,
                    ResourceId = newRow.ResourceId!.Value,
                    StreamStatus = newRow.StreamStatus!.Value,
                    WasInserted = true,
                };
            }
        }

        // =====================================================================
        // BULK UPSERT
        // Used by: TicketRepo.CreateTicketAsync and UpdateTicketAsync
        // Loops over all ResourceIds and calls single upsert for each one.
        // All share the same DbContext + transaction from the caller.
        // =====================================================================
        public async Task<List<WorkStreamResult>> UpsertWorkStreamsAsync(
            Guid? issueId,
            List<Guid> resourceIds,
            int streamStatus,
            decimal? completionPct,
            DateTime? targetDate)
        {
            var results = new List<WorkStreamResult>();

            foreach (var resourceId in resourceIds)
            {
                // Call single upsert for each assignee
                // Each person's row is independent — no conflict between them
                var result = await UpsertWorkStreamAsync(new WorkStreamContext
                {
                    IssueId = issueId,
                    ResourceId = resourceId,
                    StreamStatus = streamStatus,
                    CompletionPct = completionPct,
                    TargetDate = targetDate,
                });

                results.Add(result);
            }

            return results;
        }


        public async Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds)
        {
            if (!removedResourceIds.Any()) return;

            var rowsToDeactivate = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    removedResourceIds.Contains(ws.ResourceId!.Value) &&
                    ws.StreamStatus != WorkStreamStatus.Inactive)   // don't touch already inactive
                .ToListAsync();

            if (!rowsToDeactivate.Any()) return;

            foreach (var row in rowsToDeactivate)
            {
                row.StreamStatus = WorkStreamStatus.Inactive;
                // CompletionPct kept as-is — preserves their last known progress
                // UpdatedAt, UpdatedBy → DBContext audit on SaveChangesAsync
            }

            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // CLEAR ALL
        // Used by: TicketRepo.UpdateTicketAsync when ResourceIds = [] (empty list)
        // Soft-closes all active WorkStream rows for this ticket.
        // Rows stay in DB (history preserved) — just status set to 4 (Completed).
        // =====================================================================
        public async Task ClearWorkStreamsAsync(Guid issueId)
        {
            var activeRows = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != WorkStreamStatus.Completed)
                .ToListAsync();

            if (!activeRows.Any()) return;

            foreach (var row in activeRows)
            {
                row.StreamStatus = WorkStreamStatus.Completed;
                row.CompletionPct = 100;
                // UpdatedAt, UpdatedBy → DBContext audit on SaveChangesAsync
            }

            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // PRIVATE: Department name lookup
        // Reads EMPLOYEEMASTER.Team of the given ResourceId (the assignee).
        // Falls back to "General" if not found or Team is empty.
        // =====================================================================
        private async Task<string> GetDepartmentNameAsync(Guid resourceId)
        {
            var employee = await _db.eMPLOYEEMASTERs
                .Where(e => e.EmployeeID == resourceId)
                .Select(e => new { e.Team })
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(employee?.Team)
                ? "General"
                : employee.Team.Trim();
        }
    }
}
