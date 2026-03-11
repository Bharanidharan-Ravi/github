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
    public class WorkStreamService : IWorkStreamService
    {
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly APIGatewayDBContext _db;

        // StreamNames where the current user IS the resource doing the work.
        // No separate ResourceId needed — resolved to _loginContext.userId.
        private static readonly HashSet<string> _selfResourceStreams =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "IN_PROGRESS",
                "HOLD",
                "AWAITING_CLIENT",
                "DEVELOPMENT",
                "TESTING",
                "REVIEW",
                "CLIENT_REVIEW",
            };

        // StreamStatus constants
        private const int STATUS_ACTIVE = 1;
        private const int STATUS_COMPLETED = 2;

        public WorkStreamService(
            IDomainService domainService,
            ILoginContextService loginContext,
            APIGatewayDBContext db)
        {
            _domainService = domainService;
            _loginContext = loginContext;
            _db = db;
        }

        // ── MAIN ENTRY POINT ─────────────────────────────────────────────────
        public async Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx)
        {
            // ── STEP 1: Resolve StreamName from Department if not supplied ────
            // If caller passed null → look up EMPLOYEEMASTER.Team for current user
            // If caller passed a value → use it as-is (owner forcing a stage)
            var streamName = ctx.StreamName;
            if (string.IsNullOrWhiteSpace(streamName))
            {
                streamName = await GetDepartmentStreamNameAsync(_loginContext.userId);
            }

            // ── STEP 2: Resolve ResourceId ────────────────────────────────────
            // Self-resource streams: current user IS the worker → use their userId
            // Other streams: use whatever was passed in ctx.ResourceId
            var resolvedResourceId = _selfResourceStreams.Contains(streamName)
                ? _loginContext.userId
                : ctx.ResourceId;

            // ── STEP 3: Close previous stream if user is changing phase ───────
            // Scope: ONLY this user's active rows on this ticket.
            // Other assignees' rows are completely untouched.
            //
            // "Changing phase" = user has an active row with a DIFFERENT StreamName.
            // Same StreamName = no close needed, just update %.
            var myPreviousActiveStream = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == ctx.IssueId &&
                    ws.ResourceId == _loginContext.userId &&  // ONLY my rows
                    ws.StreamStatus == STATUS_ACTIVE &&
                    ws.StreamName != streamName)                  // different phase
                .FirstOrDefaultAsync();

            if (myPreviousActiveStream != null)
            {
                // Close my old phase — set completed
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.Id == myPreviousActiveStream.Id,
                    ws =>
                    {
                        ws.StreamStatus = STATUS_COMPLETED;
                        ws.CompletionPct = 100;
                        // UpdatedAt, UpdatedBy → DBContext audit
                    }
                );
            }

            // ── STEP 4: Upsert — check MY last row for this IssueId ──────────
            // Key insight: scoped to ResourceId = current user.
            // This is what prevents multi-assignee conflicts.
            // Assignee A checks their own last row.
            // Assignee B checks their own last row.
            // They never read or modify each other's rows.
            var myLastStream = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == ctx.IssueId &&
                    ws.ResourceId == _loginContext.userId)     // ONLY my rows
                .OrderByDescending(ws => ws.CreatedAt)
                .FirstOrDefaultAsync();

            // Same StreamName as my last row → update CompletionPct only
            var existingStream = (myLastStream?.StreamName == streamName)
                ? myLastStream
                : null;

            WorkStream workStream;

            if (existingStream != null)
            {
                // ── UPDATE: same phase, just update progress ──────────────────
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.Id == existingStream.Id,
                    ws =>
                    {
                        ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;
                        ws.StreamStatus = STATUS_ACTIVE;  // re-activate if it was closed
                        // StreamName, ResourceId, TargetDate → NOT touched
                    }
                );

                workStream = existingStream;

                return new WorkStreamResult
                {
                    Id = workStream.Id,
                    StreamName = workStream.StreamName,
                    ResourceId = workStream.ResourceId,
                    WasInserted = false,
                };
            }
            else
            {
                // ── INSERT: new phase for this user ───────────────────────────
                workStream = new WorkStream
                {
                    IssueId = ctx.IssueId,
                    StreamName = streamName,
                    ResourceId = resolvedResourceId,
                    StreamStatus = STATUS_ACTIVE,
                    CompletionPct = ctx.CompletionPct ?? 0,
                    TargetDate = ctx.TargetDate,
                    // CreatedAt, UpdatedAt, CreatedBy, UpdatedBy → DBContext audit
                };

                await _domainService.SaveEntityWithAttachmentsAsync(workStream, null);
                // After SaveChangesAsync → workStream.Id is now the DB GUID

                return new WorkStreamResult
                {
                    Id = workStream.Id,
                    StreamName = workStream.StreamName,
                    ResourceId = workStream.ResourceId,
                    WasInserted = true,
                };
            }
        }

        // ── DEPARTMENT LOOKUP ─────────────────────────────────────────────────
        // Reads EMPLOYEEMASTER.Team for the given userId.
        // Team = "Development", "QA", "DevOps" etc. → used as StreamName directly.
        // Falls back to "GENERAL" if employee record not found or Team is empty.
        private async Task<string> GetDepartmentStreamNameAsync(Guid userId)
        {
            var employee = await _db.eMPLOYEEMASTERs
                .Where(e => e.EmployeeID == userId)
                .Select(e => new { e.Team })
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(employee?.Team)
                ? "GENERAL"
                : employee.Team.Trim().ToUpperInvariant();
            // ToUpperInvariant → consistent with _selfResourceStreams HashSet comparison
        }
    }
}
