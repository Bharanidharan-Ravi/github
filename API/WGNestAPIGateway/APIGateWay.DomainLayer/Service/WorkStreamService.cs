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
    // WorkStreamService — pure domain logic, NO SignalR
    //
    // Architecture:
    //   Service  → DB writes, status compute, RepoKey lookup, payload build
    //   Repo     → calls service, then does SignalR broadcasts
    //   Controller → validation only, delegates to Repo
    //
    // PK naming matches your entity: StreamId (Guid, IDENTITY)
    // StatusMaster PK: Status_Id
    // StatusId values from your statusmasterEntity.cs:
    //   New=1, Assigned=2, InDevelopment=5, DevelopmentCompleted=6,
    //   UnitTesting=7, FunctionalTesting=8, UATTesting=9,
    //   FunctionalFixCompleted=11, MovedToProduction=12,
    //   OnHold=13, Closed=14, Cancelled=15, Inactive=16
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
        // PUBLIC ENTRY POINT — reads like a table of contents
        // =====================================================================
        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // Poster is always the logged-in user
                    var posterId = dto.ResourceId ?? _loginContext.userId;

                    // ── TYPE 1: Pure assignment — no thread, no % update ──────
                    if (dto.AssignOnly)
                    {
                        if (!dto.NextAssigneeId.HasValue)
                            throw new InvalidOperationException(
                                "NextAssigneeId is required when AssignOnly is true.");

                        var assigned = await AssignWorkStreamAsync(
                            issueId: dto.IssueId,
                            assigneeId: dto.NextAssigneeId.Value,
                            streamStatusId: dto.NextAssigneeStreamId,
                            targetDate: dto.TargetDate
                        );

                        var ticketStatusForAssign =
                            await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                        return new PostWorkStreamResponse
                        {
                            WorkStreamId = assigned.StreamId,
                            ResourceId = assigned.ResourceId ?? Guid.Empty,
                            StreamName = assigned.StreamName ?? string.Empty,
                            StreamStatus = assigned.StreamStatus,
                            StatusName = "New",
                            CompletionPct = 0,
                            ThreadCreated = false,
                            ThreadId = null,
                            TicketStatusId = ticketStatusForAssign.ComputedStatusId,
                            TicketStatusName = ticketStatusForAssign.ComputedStatusName,
                            TicketOverallPct = ticketStatusForAssign.OverallPct,
                            TotalSubtasks = ticketStatusForAssign.TotalSubtasks,
                            CompletedSubtasks = ticketStatusForAssign.CompletedSubtasks,
                            ActiveSubtasks = ticketStatusForAssign.ActiveSubtasks,
                            TicketCompleted = ticketStatusForAssign.TicketAutoCompleted,
                            IssueId = dto.IssueId,
                            RepoKey = ticketStatusForAssign.RepoKey,
                            IsTerminal = ticketStatusForAssign.IsTerminal,
                            BroadcastPayload = ticketStatusForAssign.BroadcastPayload,
                        };
                    }

                    // ── TYPE 2 / 3: Progress update ───────────────────────────
                    var resolvedStreamName = string.IsNullOrWhiteSpace(dto.StreamName)
                        ? await GetDepartmentNameAsync(posterId)
                        : dto.StreamName;
                    // Auto-resolve StreamStatus from StreamName + CompletionPct
                    // UI sends StreamName ("Web Development", "QA Testing" etc.)
                    // Service computes which StatusId that maps to
                    var resolvedStatus = dto.StreamStatus.HasValue
                         ? dto.StreamStatus.Value                                           // UI sent explicit status → use it
                         : ResolveStreamStatus(dto.StreamName, dto.CompletionPct ?? 0);

                    // 1. Thread — optional (null/no comment = no thread)
                    var (threadId, threadCreated) =
                        await HandleThreadAsync(dto, posterId, attachmentResult);

                    // 2. Test failure / clear (no SaveChangesAsync inside — EF tracks)
                    if (dto.ReportTestFailure)
                        await HandleTestFailureAsync(dto, posterId);

                    if (dto.ClearTestFailure)
                        await HandleClearFailureAsync(dto, posterId);

                    // 3. Validate before upsert (block DevCompleted if test failed)
                    await ValidateStatusTransitionAsync(resolvedStatus, posterId, dto.IssueId);

                    // 4. Upsert poster's own WorkStream row
                    // ── Determine whether to create poster's own row or just assign ──────────
                    bool isPureAssignment = dto.NextAssigneeId.HasValue &&
                                            dto.CompletionPct == null &&
                                            dto.StreamStatus == null;

                    WorkStream stream;

                    if (isPureAssignment)
                    {
                        // Owner assigning someone — no row for the owner, just create assignee row
                        stream = await AssignWorkStreamAsync(
                            issueId: dto.IssueId,
                            assigneeId: dto.NextAssigneeId.Value,
                            streamStatusId: dto.NextAssigneeStreamId,
                            targetDate: dto.TargetDate
                        );
                    }
                    else
                    {
                        // Normal progress post — update poster's own row
                        stream = await UpsertStreamAsync(
                            dto, posterId, resolvedStatus, threadId, resolvedStreamName);

                        // Also assign next person if provided
                        if (dto.NextAssigneeId.HasValue)
                        {
                            await AssignWorkStreamAsync(
                                issueId: dto.IssueId,
                                assigneeId: dto.NextAssigneeId.Value,
                                streamStatusId: dto.NextAssigneeStreamId,
                                targetDate: dto.TargetDate
                            );
                        }
                    }



                    // 6. Compute live ticket status (updates DB + builds broadcast payload)
                    var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                    return BuildResponse(
                        dto, stream, resolvedStatus, threadId, threadCreated, ticketStatus);
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
        // AUTO-RESOLVE StreamStatus from StreamName + CompletionPct
        //
        // Uses your StatusId values:
        //   Developer: 100% → DevelopmentCompleted(6), <100% → InDevelopment(5)
        //   Tester:    100% → FunctionalFixCompleted(11), <100% → FunctionalTesting(8)
        //   Other:     100% → Closed(14), <100% → InDevelopment(5)
        // =====================================================================
        private static int ResolveStreamStatus(string streamName, decimal completionPct)
        {
            var name = (streamName ?? string.Empty).ToUpperInvariant();

            bool isDeveloper = name.Contains("DEV") || name.Contains("DEVELOP");
            bool isTester = name.Contains("TEST") || name.Contains("QA") ||
                               name.Contains("FUNCTIONAL") || name.Contains("QUALITY");

            if (isDeveloper)
                return completionPct >= 100
                    ? StatusId.DevelopmentCompleted   // 6
                    : StatusId.InDevelopment;         // 5

            if (isTester)
                return completionPct >= 100
                    ? StatusId.FunctionalFixCompleted // 11
                    : StatusId.FunctionalTesting;     // 8

            // Fallback
            return completionPct >= 100
                ? StatusId.Closed        // 14
                : StatusId.InDevelopment; // 5
        }

        // =====================================================================
        // 1. HANDLE THREAD
        // Returns (threadId=0, false) when no thread needed (pure % update)
        // =====================================================================
        private async Task<(int threadId, bool threadCreated)> HandleThreadAsync(
            PostWorkStreamDto dto,
            Guid posterId,
            ProcessedAttachmentResult? attachmentResult)
        {
            // Toggle ON: link the last thread this user posted
            if (dto.UseLastThread == true)
            {
                var last = await _db.ISSUETHREADS
                    .Where(t =>
                        t.Issue_Id == dto.IssueId &&
                        t.CreatedBy == posterId)
                    .OrderByDescending(t => t.ThreadId)
                    .FirstOrDefaultAsync();

                if (last == null)
                    throw new InvalidOperationException(
                        "No previous thread found. Disable the toggle and add a comment.");

                return (last.ThreadId, false);
            }

            // Comment provided: create new thread with optional attachments
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
                    From_Time = dto.From_Time,   // null = not logged
                    To_Time = dto.To_Time,
                    Hours = dto.Hours,
                };

                await _domainService.SaveEntityWithAttachmentsAsync(
                    thread, attachmentResult?.Attachments);

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                    await _attachmentService.CleanupTempFiles(dto.temp);

                return (threadId, true);
            }

            // Neither: pure % update — no thread at all
            return (0, false);
        }

        // =====================================================================
        // 2a. HANDLE TEST FAILURE
        // Mutates tracked entities only — NO SaveChangesAsync
        // EF batches these with the next SaveChangesAsync inside UpsertStreamAsync
        // =====================================================================
        private async Task HandleTestFailureAsync(PostWorkStreamDto dto, Guid testerResourceId)
        {
            var query = _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    ws.ResourceId != testerResourceId); // exclude tester's own row

            if (dto.TargetDeveloperResourceId.HasValue)
                query = query.Where(ws =>
                    ws.ResourceId == dto.TargetDeveloperResourceId.Value);

            var rows = await query.ToListAsync();

            // Soft fail — if no dev rows yet, skip silently
            // Block will apply when developers post their first thread
            if (!rows.Any()) return;

            foreach (var row in rows)
            {
                row.CompletionPct = Math.Max(0,
                    (row.CompletionPct ?? 0) - Math.Max(1, dto.PercentageDrop ?? 30));
                row.BlockedByTestFailure = true;
                row.BlockedReason = dto.TestFailureComment;
                row.BlockedAt = DateTime.UtcNow;
                row.BlockedByResourceId = testerResourceId;

                // Revert DevCompleted → InDevelopment (cannot stay completed while blocked)
                //if (row.StreamStatus == StatusId.DevelopmentCompleted)
                row.StreamStatus = StatusId.InDevelopment;

                // NO SaveChangesAsync — EF change tracker holds these
            }
        }

        // =====================================================================
        // 2b. HANDLE CLEAR FAILURE
        // Validates who can clear, then mutates — NO SaveChangesAsync
        // =====================================================================
        private async Task HandleClearFailureAsync(PostWorkStreamDto dto, Guid clearingResourceId)
        {
            // Check 1: is this person the original blocker?
            var isBlocker = await _db.WorkStreams
                .AnyAsync(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.BlockedByResourceId == clearingResourceId);

            // Check 2: is this person an active tester on this ticket?
            var isActiveTester = await _db.WorkStreams
            .AnyAsync(ws =>
                ws.IssueId == dto.IssueId &&
                ws.ResourceId == clearingResourceId &&
                ws.StreamStatus != null &&
                ws.StreamStatus != StatusId.Inactive &&
                ws.StreamStatus != StatusId.Cancelled &&
                (ws.StreamStatus == StatusId.FunctionalTesting ||
                 ws.StreamStatus == StatusId.UATTesting ||
                 ws.StreamStatus == StatusId.FunctionalFixCompleted ||
                 ws.StreamStatus == StatusId.UnitTesting));

            // Check 3: is this person the ticket owner?
            var isOwner = await _db.Set<TicketMaster>()
                .AnyAsync(t =>
                    t.Issue_Id == dto.IssueId &&
                    t.CreatedBy == clearingResourceId);

            if (!isBlocker && !isActiveTester && !isOwner)
                throw new InvalidOperationException(
                    "You are not authorised to clear this test failure. " +
                    "Only the tester who reported the failure, another active " +
                    "tester on this ticket, or the ticket owner can unblock a developer.");

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
                // NO SaveChangesAsync — EF tracks
            }
        }

        // =====================================================================
        // 3. VALIDATE STATUS TRANSITION
        // Blocks DevCompleted when test failure is open
        // =====================================================================
        private async Task ValidateStatusTransitionAsync(
            int resolvedStatus, Guid posterId, Guid? issueId)
        {
            if (resolvedStatus != StatusId.DevelopmentCompleted) return;

            var row = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive)
                .FirstOrDefaultAsync();

            if (row?.BlockedByTestFailure == true)
                throw new InvalidOperationException(
                    $"Cannot mark Development Completed. Testing failed: " +
                    $"{row.BlockedReason ?? "bugs reported"}. " +
                    "The tester must verify the fix and clear the failure flag first.");
        }

        // =====================================================================
        // 4. UPSERT STREAM — insert or update poster's WorkStream row
        // =====================================================================


        private async Task<WorkStream> UpsertStreamAsync(
        PostWorkStreamDto dto,
        Guid posterId,
        int resolvedStatus,
        int threadId,
        string resolvedStreamName)
        {
            // ── Step 1: find active (non-completed) row for this stage ────────────
            var stageRow = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamName == resolvedStreamName &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value));

            if (stageRow != null)
            {
                // Active row exists — normal update
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == stageRow.StreamId,
                    ws =>
                    {
                        ws.StreamStatus = resolvedStatus;
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
                return stageRow;
            }

            // ── Step 2: no active row — check for existing COMPLETED row ─────────
            // This covers the case: User A already at 100% DevCompleted, posts again
            // Do NOT create a new row — just update the existing completed row
            var completedRow = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamName == resolvedStreamName &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value));

            if (completedRow != null)
            {
                // ── Already completed — only update thread/time, block status downgrade
                // If they post a NEW completed/same status → update thread link only
                // If they try to go BACK to active status → block it
                bool isDowngrade = !StatusId.CompletedStatuses.Contains(resolvedStatus);

                if (isDowngrade)
                    throw new InvalidOperationException(
                        $"Your {resolvedStreamName} task is already completed. " +
                        "You cannot move it back to an active status. " +
                        "Contact the owner if this stage needs to be reopened.");

                // Update thread link and time fields only — keep status/% as completed
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == completedRow.StreamId,
                    ws =>
                    {
                        // Only update thread — never downgrade status or %
                        if (threadId > 0)
                            ws.ThreadId = threadId;

                        if (dto.TargetDate.HasValue)
                            ws.TargetDate = dto.TargetDate;

                        // Status and CompletionPct are NOT touched — row stays completed
                    }
                );
                return completedRow;
            }

            // ── Step 3: truly no row exists — INSERT new row ──────────────────────
            var newRow = new WorkStream
            {
                IssueId = dto.IssueId,
                StreamName = resolvedStreamName,
                ResourceId = posterId,
                StreamStatus = resolvedStatus,
                CompletionPct = dto.CompletionPct ?? 0,
                TargetDate = dto.TargetDate,
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = threadId > 0 ? threadId : null,
            };

            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
            await EnsureTicketAssignedAsync(dto.IssueId);
            return newRow;
        }
        //private async Task<WorkStream> UpsertStreamAsync(
        //    PostWorkStreamDto dto,
        //    Guid posterId,
        //    int resolvedStatus,
        //    int threadId)
        //{
        //    var existing = await _db.WorkStreams
        //        .FirstOrDefaultAsync(ws =>
        //            ws.IssueId == dto.IssueId &&
        //            ws.ResourceId == posterId &&
        //            ws.StreamStatus != null &&
        //            ws.StreamStatus != StatusId.Inactive &&
        //            ws.StreamStatus != StatusId.Cancelled);

        //    if (existing != null)
        //    {
        //        await _domainService.UpdateTrackedEntityAsync<WorkStream>(
        //            ws => ws.StreamId == existing.StreamId,
        //            ws =>
        //            {
        //                ws.StreamName = string.IsNullOrWhiteSpace(dto.StreamName)
        //                    ? ws.StreamName
        //                    : dto.StreamName;
        //                ws.StreamStatus = resolvedStatus;
        //                ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

        //                if (threadId > 0)
        //                {
        //                    ws.ThreadId = threadId;
        //                    // First thread ever = scope/parent thread — set only once
        //                    if (ws.ParentThreadId == null)
        //                        ws.ParentThreadId = threadId;
        //                }

        //                if (dto.TargetDate.HasValue)
        //                    ws.TargetDate = dto.TargetDate;

        //                // UpdatedAt, UpdatedBy → DBContext audit
        //            }
        //        );

        //        return existing;
        //    }
        //    else
        //    {
        //        // New row — resolve StreamName from poster's department
        //        var streamName = await GetDepartmentNameAsync(posterId);

        //        var newRow = new WorkStream
        //        {
        //            IssueId = dto.IssueId,
        //            StreamName = string.IsNullOrWhiteSpace(dto.StreamName)
        //                ? streamName
        //                : dto.StreamName,
        //            ResourceId = posterId,
        //            StreamStatus = resolvedStatus,
        //            CompletionPct = dto.CompletionPct ?? 0,
        //            TargetDate = dto.TargetDate,
        //            ThreadId = threadId > 0 ? threadId : null,
        //            ParentThreadId = threadId > 0 ? threadId : null,
        //            // CreatedAt, CreatedBy, UpdatedAt, UpdatedBy → DBContext audit
        //        };

        //        await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);

        //        // New subtask → move ticket New(1) → Assigned(2)
        //        await EnsureTicketAssignedAsync(dto.IssueId);

        //        return newRow;
        //    }
        //}

        // =====================================================================
        // ASSIGN WORK STREAM — create row for a person without any thread
        // Used for:
        //   - AssignOnly=true (owner assigns directly)
        //   - Developer 100% → pass to tester (NextAssigneeId)
        //   - Reassigning after tester removed
        // =====================================================================


        public async Task<WorkStream> AssignWorkStreamAsync(
            Guid issueId,
            Guid assigneeId,
            int? streamStatusId,   // Status_Master.Id — e.g. 7 = Unit Testing
            DateTime? targetDate)
        {
            // ── Step 1: resolve StreamName from Status_Master ──────────────────────
            // e.g. streamStatusId=7 → Status_Name="Unit Testing" → StreamName="Unit Testing"
            string finalStreamName;

            //if (streamStatusId.HasValue)
            //{
            //    var statusName = await _db.StatusMasters
            //        .Where(s => s.Status_Id == streamStatusId.Value)
            //        .Select(s => s.Status_Name)
            //        .FirstOrDefaultAsync();

            //    // If status found → use Status_Name as the stage label
            //    // If not found   → fall back to employee's department
            //    finalStreamName = string.IsNullOrWhiteSpace(statusName)
            //        ? await GetDepartmentNameAsync(assigneeId)
            //        : statusName;
            //}
            //else
            //{
                // No status passed → use employee's department name
                finalStreamName = await GetDepartmentNameAsync(assigneeId);
            //}

            // ── Idempotent check: same person + same stage = no duplicate ─────────
            // Different stage = new row (this is the key change that fixes the bug)
            var existing = await _db.WorkStreams
              .FirstOrDefaultAsync(ws =>
                  ws.IssueId == issueId &&
                  ws.ResourceId == assigneeId &&
                  ws.StreamName == finalStreamName &&
                  ws.StreamStatus != StatusId.Inactive &&
                  ws.StreamStatus != StatusId.Cancelled &&
                  !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value));

            if (existing != null)
            {
                // Already has this exact stage assigned — return as-is
                return existing;
            }

            // ── INSERT new row — Status=New(1), %=0, no thread ────────────────────
            var newRow = new WorkStream
            {
                IssueId = issueId,
                StreamName = finalStreamName,
                ResourceId = assigneeId,
                StreamStatus = streamStatusId ?? StatusId.New,    // 1 — not started yet
                CompletionPct = 0,
                TargetDate = targetDate,
                ThreadId = null,
                ParentThreadId = null,
            };

            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
            await EnsureTicketAssignedAsync(issueId);

            return newRow;
        }

        //public async Task<WorkStream> AssignWorkStreamAsync(
        //    Guid issueId,
        //    Guid assigneeId,
        //    int? streamName,
        //    DateTime? targetDate)
        //{

        //    // Idempotent — return existing active row if already assigned
        //    var existing = await _db.WorkStreams
        //       .FirstOrDefaultAsync(ws =>
        //           ws.IssueId == issueId &&
        //           ws.ResourceId == assigneeId &&
        //           ws.StreamStatus != StatusId.Inactive &&
        //           ws.StreamStatus != StatusId.Cancelled);

        //    // Resolve StreamName from assignee's department if not provided
        //    var deptName = await GetDepartmentNameAsync(assigneeId);
        //    var finalStreamName = deptName;

        //    if (existing != null)
        //    {
        //        // Already assigned — update StreamName if changed, leave % and status
        //        if (existing.StreamName != finalStreamName)
        //        {
        //            await _domainService.UpdateTrackedEntityAsync<WorkStream>(
        //                ws => ws.StreamId == existing.StreamId,
        //                ws => { ws.StreamName = finalStreamName; }
        //            );
        //        }
        //        return existing;
        //    }

        //    // INSERT new row — Status=New(1), %=0, no thread
        //    var newRow = new WorkStream
        //    {
        //        IssueId = issueId,
        //        StreamName = finalStreamName,
        //        ResourceId = assigneeId,
        //        StreamStatus = StatusId.New,   // 1
        //        CompletionPct = 0,
        //        TargetDate = targetDate,
        //        ThreadId = null,           // no thread — assignment only
        //        ParentThreadId = null,           // set when they post first thread
        //    };

        //    await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
        //    await EnsureTicketAssignedAsync(issueId);

        //    return newRow;
        //}

        // =====================================================================
        // COMPUTE AND UPDATE TICKET STATUS
        //
        // Pure domain: updates Ticket.Status + Ticket.CompletionPct in DB
        // Returns TicketStatusResult with RepoKey + BroadcastPayload
        // The CALLER (WorkStreamRepo) does the actual SignalR broadcast
        // =====================================================================
        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid? issueId)
        {
            // Load all non-inactive subtasks joined with Status_Master for Sort_Order
            //var subtasks = await _db.WorkStreams
            //    .Where(ws =>
            //        ws.IssueId == issueId &&
            //        ws.StreamStatus != null &&
            //        ws.StreamStatus != StatusId.Inactive &&
            //        ws.StreamStatus != StatusId.Cancelled)
            //    .Join(_db.StatusMasters,
            //        ws => ws.StreamStatus,
            //        sm => sm.Status_Id,
            //        (ws, sm) => new
            //        {
            //            ws.StreamStatus,
            //            ws.CompletionPct,
            //            sm.Sort_Order,
            //            sm.Status_Name,
            //            IsCompleted = StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value),
            //        })
            //    .ToListAsync();

            var subtasks = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != StatusId.Inactive &&   // only exclude explicitly removed
                    ws.StreamStatus != StatusId.Cancelled)
                .Join(_db.StatusMasters,
                    ws => ws.StreamStatus ?? StatusId.New,   // ← NULL → New(1) for join
                    sm => sm.Status_Id,
                    (ws, sm) => new
                    {
                        ws.StreamStatus,
                        ws.CompletionPct,
                        sm.Sort_Order,
                        sm.Status_Name,
                        IsCompleted = ws.StreamStatus.HasValue &&
                                      StatusId.CompletedStatuses.Contains(ws.StreamStatus.Value),
                        // NULL StreamStatus = not completed, always active
                    })
                .ToListAsync();
            if (!subtasks.Any())
                return new TicketStatusResult
                {
                    ComputedStatusId = StatusId.New,
                    ComputedStatusName = "New",
                    OverallPct = 0,
                    TotalSubtasks = 0,
                    CompletedSubtasks = 0,
                    ActiveSubtasks = 0,
                    TicketAutoCompleted = false,
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
                computedStatusId = StatusId.Closed;  // 14
                computedStatusName = "Closed";
            }
            else
            {
                // Most advanced ACTIVE stage = highest Sort_Order among non-completed
                var mostAdvanced = subtasks
                    .Where(s => !s.IsCompleted)
                    .OrderByDescending(s => s.Sort_Order)
                    .First();

                computedStatusId = mostAdvanced.StreamStatus!.Value;
                computedStatusName = mostAdvanced.Status_Name;
            }

            // Load ticket — update Status + CompletionPct
            var ticket = await _db.Set<TicketMaster>()
                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);
            bool isTerminal =
                ticket?.Status == StatusId.Closed ||
                ticket?.Status == StatusId.Cancelled;

            // If ticket was closed but now has active subtasks → reopen it
            bool shouldReopen = isTerminal && activeSubtasks > 0;

            if (ticket != null && (!isTerminal || shouldReopen))
            {
                await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                    t => t.Issue_Id == issueId,
                    t =>
                    {
                        t.Status = computedStatusId;   // new computed status
                        t.CompletionPct = (decimal?)overallPct; // new average %
                        t.StatusName = computedStatusName;
                    }
                );
            }
            if (shouldReopen)
                isTerminal = false;

            // Resolve RepoKey for broadcast — done here so Repo doesn't need extra DB call
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

            // Pre-build broadcast payload — Repo passes this directly to BroadcastAsync
            var broadcastPayload = isTerminal ? null : (object)new
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
                RepoKey = repoKey,
                IsTerminal = isTerminal,
                BroadcastPayload = broadcastPayload,
            };
        }

        // =====================================================================
        // SINGLE UPSERT — called from ThreadRepo / TicketRepo
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

                var ticketStatus1 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = existing.StreamId,
                    StreamName = existing.StreamName,
                    ResourceId = existing.ResourceId!.Value,
                    StreamStatus = ctx.StreamStatus,
                    WasInserted = false,
                    IsBlocked = existing.BlockedByTestFailure,
                    BlockedReason = existing.BlockedReason,
                    TicketStatus = ticketStatus1,
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

                var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = newRow.StreamId,
                    StreamName = newRow.StreamName,
                    ResourceId = newRow.ResourceId!.Value,
                    StreamStatus = newRow.StreamStatus,
                    WasInserted = true,
                    TicketStatus = ticketStatus2,
                };
            }
        }

        // =====================================================================
        // BULK UPSERT — TicketRepo (multiple assignees)
        // =====================================================================
        public async Task<List<WorkStreamResult>> UpsertWorkStreamsAsync(
            Guid? issueId,
            List<Guid> resourceIds,
            int? streamStatus,
            decimal? completionPct,
            DateTime? targetDate)
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
        // MARK INACTIVE — specific people removed from ticket
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

            // Auto-clear blocks that were set by any removed tester
            // Prevents permanent block if tester was removed before clearing
            var devBlocksToRelease = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.BlockedByTestFailure == true &&
                    ws.BlockedByResourceId != null &&
                    removedResourceIds.Contains(ws.BlockedByResourceId!.Value))
                .ToListAsync();

            foreach (var row in devBlocksToRelease)
            {
                row.BlockedByTestFailure = false;
                row.BlockedReason = null;
                row.BlockedAt = null;
                row.BlockedByResourceId = null;
            }

            if (devBlocksToRelease.Any())
                await _db.SaveChangesAsync();
        }

        // =====================================================================
        // CLEAR ALL — ResourceIds = [] on ticket update
        // =====================================================================
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
            int resolvedStatus,
            int threadId,
            bool threadCreated,
            TicketStatusResult ticketStatus)
        {
            return new PostWorkStreamResponse
            {
                // WorkStream subtask
                WorkStreamId = stream.StreamId,
                ResourceId = stream.ResourceId ?? Guid.Empty,
                StreamName = stream.StreamName ?? dto.StreamName,
                StreamStatus = resolvedStatus,
                CompletionPct = dto.CompletionPct ?? stream.CompletionPct ?? 0,
                IsBlocked = stream.BlockedByTestFailure,
                BlockedReason = stream.BlockedReason,

                // Thread
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = stream.ParentThreadId,
                ThreadCreated = threadCreated,

                // Ticket live status
                TicketStatusId = ticketStatus.ComputedStatusId,
                TicketStatusName = ticketStatus.ComputedStatusName,
                TicketOverallPct = ticketStatus.OverallPct,
                TotalSubtasks = ticketStatus.TotalSubtasks,
                CompletedSubtasks = ticketStatus.CompletedSubtasks,
                ActiveSubtasks = ticketStatus.ActiveSubtasks,
                TicketCompleted = ticketStatus.TicketAutoCompleted,

                // Test failure / unblock
                DeveloperBlocked = dto.ReportTestFailure,
                DeveloperUnblocked = dto.ClearTestFailure,
                BlockSummary = dto.ReportTestFailure
                    ? $"Developer blocked: {dto.TestFailureComment}"
                    : dto.ClearTestFailure
                        ? "Developer unblocked — can now mark development completed."
                        : null,

                // Broadcast data (used by WorkStreamRepo, not sent to UI)
                IssueId = dto.IssueId,
                RepoKey = ticketStatus.RepoKey,
                IsTerminal = ticketStatus.IsTerminal,
                BroadcastPayload = ticketStatus.BroadcastPayload,
            };
        }
    }
}