using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.BusinessLayer.Repository
{
    // =========================================================================
    // WorkStreamService — pure domain logic
    //
    // NO IRealtimeNotifier here.
    // NO IHelperGetData here.
    //
    // ComputeAndUpdateTicketStatusAsync returns TicketStatusResult which
    // includes RepoKey + BroadcastPayload pre-built.
    // The CALLER (Business Layer repo or controller) does the actual broadcast.
    //
    // Constructor now only has domain-layer dependencies:
    //   APIGatewayDBContext, IDomainService, ILoginContextService,
    //   APIGateWayCommonService, IAttachmentService
    // =========================================================================

    public class WorkStreamService : IWorkStreamService
    {
        private readonly APIGatewayDBContext _db;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly APIGateWayCommonService _commonService;
        private readonly IAttachmentService _attachmentService;

        public WorkStreamService(
            APIGatewayDBContext db,
            IDomainService domainService,
            ILoginContextService loginContext,
            APIGateWayCommonService commonService,
            IAttachmentService attachmentService)
        {
            _db = db;
            _domainService = domainService;
            _loginContext = loginContext;
            _commonService = commonService;
            _attachmentService = attachmentService;
        }

        // =====================================================================
        // PUBLIC ENTRY POINT
        // =====================================================================
        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var resourceId = dto.ResourceId ?? _loginContext.userId;

                    // 1. Thread — optional
                    var (threadId, threadCreated) =
                        await HandleThreadAsync(dto, resourceId, attachmentResult);

                    // 2. Test failure / clear
                    if (dto.ReportTestFailure)
                        await HandleTestFailureAsync(dto, resourceId);

                    if (dto.ClearTestFailure)
                        await HandleClearFailureAsync(dto);

                    // 3. Validate before upsert
                    await ValidateStatusTransitionAsync(dto, resourceId);

                    // 4. Upsert stream row
                    var stream = await UpsertStreamAsync(dto, resourceId, threadId);

                    // 5. Compute live ticket status
                    // Returns RepoKey + BroadcastPayload — caller does the broadcast
                    var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                    return BuildResponse(dto, stream, threadId, threadCreated, ticketStatus);
                });
            }
            catch
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(
                        attachmentResult.PermanentFilePathsCreated);
                throw;
            }
        }

        // =====================================================================
        // 1. HANDLE THREAD
        // =====================================================================
        private async Task<(int threadId, bool threadCreated)> HandleThreadAsync(
            PostWorkStreamDto dto,
            Guid resourceId,
            ProcessedAttachmentResult? attachmentResult)
        {
            if (dto.UseLastThread == true)
            {
                var last = await _db.ISSUETHREADS
                    .Where(t =>
                        t.Issue_Id == dto.IssueId &&
                        t.CreatedBy == resourceId)
                    .OrderByDescending(t => t.ThreadId)
                    .FirstOrDefaultAsync();

                if (last == null)
                    throw new InvalidOperationException(
                        "No previous thread found. Disable the toggle and add a comment.");

                return (last.ThreadId, false);
            }

            if (!string.IsNullOrWhiteSpace(dto.Comment))
            {
                var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
                var threadId = seq.CurrentValue;
                string finalHtml = dto.Comment;

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var permFolder = $"{threadId}-{dto.IssueId}";
                    var relativePath = $"{permUserId}/{permFolder}";

                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        dto.Comment, dto.temp.temps, relativePath,
                        threadId.ToString(), "ThreadMaster");

                    finalHtml = attachmentResult.UpdatedHtml;
                }

                var thread = new ThreadMaster
                {
                    ThreadId = threadId,
                    Issue_Id = dto.IssueId,
                    HtmlDesc = finalHtml,
                    CommentText = HtmlUtilities.ConvertToPlainText(finalHtml),
                };

                await _domainService.SaveEntityWithAttachmentsAsync(
                    thread, attachmentResult?.Attachments);

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                    await _attachmentService.CleanupTempFiles(dto.temp);

                return (threadId, true);
            }

            return (0, false);  // pure % update — no thread
        }

        // =====================================================================
        // 2a. HANDLE TEST FAILURE
        // =====================================================================
        private async Task HandleTestFailureAsync(PostWorkStreamDto dto, Guid testerResourceId)
        {
            var query = _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    ws.ResourceId != testerResourceId);

            if (dto.TargetDeveloperResourceId.HasValue)
                query = query.Where(ws =>
                    ws.ResourceId == dto.TargetDeveloperResourceId.Value);

            var rows = await query.ToListAsync();

            // ── Soft fail — warn but don't throw ─────────────────────────────────────
            // Tester may post before developers have their rows (they post first thread later)
            // In that case: record the failure comment on the tester's response only
            // The block will apply when developers' rows are created next time they post
            if (!rows.Any()) return;

            foreach (var row in rows)
            {
                row.CompletionPct = Math.Max(0, (row.CompletionPct ?? 0) - (dto.PercentageDrop ?? 30));
                row.BlockedByTestFailure = true;
                row.BlockedReason = dto.TestFailureComment;
                row.BlockedAt = DateTime.UtcNow;
                row.BlockedByResourceId = testerResourceId;

                if (row.StreamStatus == StatusId.DevelopmentCompleted)
                    row.StreamStatus = StatusId.InDevelopment;

                // NO SaveChangesAsync here — EF change tracker holds these
                // They save when the next SaveChangesAsync fires (inside UpsertStreamAsync)
            }
        }

        // =====================================================================
        // 2b. HANDLE CLEAR FAILURE
        // =====================================================================
        private async Task HandleClearFailureAsync(PostWorkStreamDto dto)
        {
            var query = _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.BlockedByTestFailure == true);

            if (dto.TargetDeveloperResourceId.HasValue)
                query = query.Where(ws =>
                    ws.ResourceId == dto.TargetDeveloperResourceId.Value);

            var rows = await query.ToListAsync();
            if (!rows.Any()) return;

            foreach (var row in rows)
            {
                row.BlockedByTestFailure = false;
                row.BlockedReason = null;
                row.BlockedAt = null;
                row.BlockedByResourceId = null;
            }
        }

        // =====================================================================
        // 3. VALIDATE STATUS TRANSITION
        // =====================================================================
        private async Task ValidateStatusTransitionAsync(PostWorkStreamDto dto, Guid resourceId)
        {
            if (dto.StreamStatus != StatusId.DevelopmentCompleted) return;

            var row = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == resourceId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive)
                .FirstOrDefaultAsync();

            if (row?.BlockedByTestFailure == true)
                throw new InvalidOperationException(
                    $"Cannot mark DevelopmentCompleted. Testing failed: " +
                    $"{row.BlockedReason ?? "bugs reported"}. " +
                    "The tester must verify the fix and clear the failure flag first.");
        }

        // =====================================================================
        // 4. UPSERT STREAM
        // =====================================================================
        private async Task<WorkStream> UpsertStreamAsync(
            PostWorkStreamDto dto,
            Guid resourceId,
            int? threadId)
        {
            var existing = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == resourceId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled);
            var streamName = await GetDepartmentNameAsync(dto.ResourceId);

            if (existing != null)
            {
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == existing.StreamId,
                    ws =>
                    {
                        //ws.StreamName = dto./*StreamName*/;
                        ws.StreamStatus = dto.StreamStatus;
                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

                        if (threadId > 0)
                        {
                            ws.ThreadId = threadId;
                            if (ws.ParentThreadId == null)
                                ws.ParentThreadId = threadId;
                        }

                        if (dto.TargetDate.HasValue)
                            ws.TargetDate = dto.TargetDate;
                    }
                );

                return existing;
            }
            else
            {
                var newRow = new WorkStream
                {
                    IssueId = dto.IssueId,
                    StreamName = streamName,
                    ResourceId = resourceId,
                    StreamStatus = dto.StreamStatus,
                    CompletionPct = dto.CompletionPct ?? 0,
                    TargetDate = dto.TargetDate,
                    ThreadId = threadId > 0 ? threadId : null,
                    ParentThreadId = threadId > 0 ? threadId : null,
                };

                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                await EnsureTicketAssignedAsync(dto.IssueId);

                return newRow;
            }
        }

        // =====================================================================
        // 5. COMPUTE AND UPDATE TICKET STATUS
        //
        // Updates Ticket.Status + Ticket.OverallPct in DB.
        // Returns TicketStatusResult including RepoKey + BroadcastPayload.
        // The CALLER (Business Layer) does the actual SignalR broadcast.
        // =====================================================================
        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid? issueId)
        {
            // Load all non-inactive subtasks joined with Status_Master Sort_Order
            var subtasks = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled)
                .Join(_db.StatusMasters,
                    ws => ws.StreamStatus,
                    sm => sm.Status_Id,
                    (ws, sm) => new
                    {
                        ws.StreamStatus,
                        ws.CompletionPct,
                        sm.Sort_Order,
                        sm.Status_Name,
                        IsCompleted = StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value),
                    })
                .ToListAsync();

            if (!subtasks.Any())
                return new TicketStatusResult
                {
                    ComputedStatusId = StatusId.New,
                    ComputedStatusName = "New",
                    OverallPct = 0,
                };

            var overallPct = Math.Round(subtasks.Average(s => (double)(s.CompletionPct ?? 0)), 2);
            var totalSubtasks = subtasks.Count;
            var completedSubtasks = subtasks.Count(s => s.IsCompleted);
            var activeSubtasks = subtasks.Count(s => !s.IsCompleted);
            var allCompleted = completedSubtasks == totalSubtasks;

            int computedStatusId;
            string computedStatusName;

            if (allCompleted)
            {
                computedStatusId = StatusId.Closed;
                computedStatusName = "Closed";
            }
            else
            {
                var mostAdvanced = subtasks
                    .Where(s => !s.IsCompleted)
                    .OrderByDescending(s => s.Sort_Order)
                    .First();

                computedStatusId = mostAdvanced.StreamStatus!.Value;
                computedStatusName = mostAdvanced.Status_Name;
            }

            // Load ticket — update Status + OverallPct
            var ticket = await _db.Set<TicketMaster>()
                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

            bool isTerminal =
                ticket?.Status == StatusId.Closed ||
                ticket?.Status == StatusId.Cancelled;

            if (ticket != null && !isTerminal)
            {
                await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                    t => t.Issue_Id == issueId,
                    t =>
                    {
                        t.Status = computedStatusId;
                        t.CompletionPct = (decimal)overallPct;
                    }
                );
            }

            // ── Resolve RepoKey for broadcast ─────────────────────────────────
            // Done HERE in service (has DB access) so the Business Layer caller
            // doesn't need another DB call — just reads result.RepoKey
            string repoKey = string.Empty;
            try
            {
                if (ticket?.RepoId != null)
                {
                    repoKey = await _db.RepositoryMasters
                        .Where(r => r.Repo_Id == ticket.RepoId)
                        .Select(r => r.RepoKey)
                        .FirstOrDefaultAsync() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamService] RepoKey lookup failed for {issueId}: {ex.Message}");
            }

            // ── Pre-build broadcast payload ────────────────────────────────────
            // Business Layer passes this directly to BroadcastAsync — no assembly needed
            var broadcastPayload = new
            {
                Issue_Id = issueId,
                Status = computedStatusId,
                StatusName = computedStatusName,
                OverallPct = overallPct,
                TotalSubtasks = totalSubtasks,
                CompletedSubtasks = completedSubtasks,
                ActiveSubtasks = activeSubtasks,
                AutoClosed = allCompleted,
                UpdatedAt = DateTime.UtcNow,
            };

            return new TicketStatusResult
            {
                ComputedStatusId = computedStatusId,
                ComputedStatusName = computedStatusName,
                OverallPct = (decimal)overallPct,
                TotalSubtasks = totalSubtasks,
                CompletedSubtasks = completedSubtasks,
                ActiveSubtasks = activeSubtasks,
                TicketAutoCompleted = allCompleted,
                RepoKey = repoKey,       // ← ready for caller to broadcast
                IsTerminal = isTerminal,    // ← caller skips broadcast if true
                BroadcastPayload = isTerminal ? null : broadcastPayload,
            };
        }

        // =====================================================================
        // SINGLE UPSERT — called from ThreadRepo / TicketRepo directly
        // =====================================================================
        public async Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx)
        {
            var streamName = await GetDepartmentNameAsync(ctx.ResourceId);

            var existing = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == ctx.IssueId &&
                    ws.ResourceId == ctx.ResourceId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled);

            if (existing != null)
            {
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == existing.StreamId,
                    ws =>
                    {
                        ws.StreamStatus = ctx.StreamStatus;
                        ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

                        if (ctx.ParentThreadId.HasValue && ws.ParentThreadId == null)
                            ws.ParentThreadId = ctx.ParentThreadId;

                        if (ctx.TargetDate.HasValue)
                            ws.TargetDate = ctx.TargetDate;
                    }
                );

                // Returns result — caller (ThreadRepo) does broadcast
                var ticketStatus = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = existing.StreamId,
                    StreamName = existing.StreamName,
                    ResourceId = existing.ResourceId!.Value,
                    StreamStatus = ctx.StreamStatus,
                    WasInserted = false,
                    IsBlocked = existing.BlockedByTestFailure,
                    BlockedReason = existing.BlockedReason,
                    TicketStatus = ticketStatus,   // caller broadcasts this
                };
            }
            else
            {
                var newRow = new WorkStream
                {
                    IssueId = ctx.IssueId,
                    StreamName = streamName,
                    ResourceId = ctx.ResourceId,
                    StreamStatus = ctx.StreamStatus,
                    CompletionPct = ctx.CompletionPct ?? 0,
                    TargetDate = ctx.TargetDate,
                    ParentThreadId = ctx.ParentThreadId,
                };

                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                await EnsureTicketAssignedAsync(ctx.IssueId);

                var ticketStatus = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = newRow.StreamId,
                    StreamName = newRow.StreamName,
                    ResourceId = newRow.ResourceId!.Value,
                    StreamStatus = newRow.StreamStatus!.Value,
                    WasInserted = true,
                    TicketStatus = ticketStatus,   // caller broadcasts this
                };
            }
        }

        // =====================================================================
        // BULK UPSERT — TicketRepo
        // =====================================================================
        public async Task<List<WorkStreamResult>> UpsertWorkStreamsAsync(
            Guid? issueId, List<Guid> resourceIds,
            int? streamStatus, decimal? completionPct, DateTime? targetDate)
        {
            var results = new List<WorkStreamResult>();
            foreach (var resourceId in resourceIds)
            {
                results.Add(await UpsertWorkStreamAsync(new WorkStreamContext
                {
                    IssueId = issueId,
                    ResourceId = resourceId,
                    StreamStatus = streamStatus,
                    CompletionPct = completionPct,
                    TargetDate = targetDate,
                }));
            }
            return results;
        }

        // =====================================================================
        // MARK INACTIVE / CLEAR ALL — TicketRepo
        // =====================================================================
        public async Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds)
        {
            if (!removedResourceIds.Any()) return;

            var rows = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    removedResourceIds.Contains(ws.ResourceId!.Value))
                .ToListAsync();

            if (!rows.Any()) return;

            foreach (var row in rows)
                row.StreamStatus = StatusId.Inactive;

            await _db.SaveChangesAsync();
        }

        public async Task ClearWorkStreamsAsync(Guid issueId)
        {
            var rows = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled)
                .ToListAsync();

            if (!rows.Any()) return;

            foreach (var row in rows)
                row.StreamStatus = StatusId.Inactive;

            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // PRIVATE HELPERS
        // =====================================================================
        private async Task EnsureTicketAssignedAsync(Guid? issueId)
        {
            await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                t => t.Issue_Id == issueId,
                t => { if (t.Status == StatusId.New) t.Status = StatusId.Assigned; }
            );
        }

        public async Task<string> GetDepartmentNameAsync(Guid? resourceId)
        {
            var emp = await _db.eMPLOYEEMASTERs
                .Where(e => e.EmployeeID == resourceId)
                .Select(e => new { e.Team })
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(emp?.Team) ? "General" : emp.Team.Trim();
        }

        private static PostWorkStreamResponse BuildResponse(
            PostWorkStreamDto dto,
            WorkStream stream,
            long threadId,
            bool threadCreated,
            TicketStatusResult ticketStatus)
        {
            return new PostWorkStreamResponse
            {
                WorkStreamId = stream.StreamId,
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = stream.ParentThreadId,
                StreamName = dto.StreamName,
                StreamStatus = dto.StreamStatus,
                ThreadCreated = threadCreated,
                TicketStatusId = ticketStatus.ComputedStatusId,
                TicketStatusName = ticketStatus.ComputedStatusName,
                TicketOverallPct = ticketStatus.OverallPct,
                TotalSubtasks = ticketStatus.TotalSubtasks,
                CompletedSubtasks = ticketStatus.CompletedSubtasks,
                ActiveSubtasks = ticketStatus.ActiveSubtasks,
                TicketCompleted = ticketStatus.TicketAutoCompleted,
                DeveloperBlocked = dto.ReportTestFailure,
                DeveloperUnblocked = dto.ClearTestFailure,
                BlockSummary = dto.ReportTestFailure
                    ? $"Developer blocked: {dto.TestFailureComment}"
                    : dto.ClearTestFailure
                        ? "Developer unblocked."
                        : null,
            };
        }
    }
}