using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;
using System.Threading;

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

    public class WorkStreamServiceCopy : IWorkStreamService
    {
        private readonly APIGatewayDBContext _db;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly APIGateWayCommonService _commonService;
        private readonly IAttachmentService _attachmentService;

        public WorkStreamServiceCopy(
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

        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var posterId = dto.ResourceId ?? _loginContext.userId;

                    // ── TYPE 1: Pure assignment — AssignOnly=true ─────────────────────
                    if (dto.AssignOnly)
                    {
                        if (dto.NextAssignees == null || !dto.NextAssignees.Any())
                            throw new InvalidOperationException(
                                "NextAssignees is required when AssignOnly is true.");

                        WorkStream lastAssigned = null;
                        foreach (var assignee in dto.NextAssignees)
                        {
                            lastAssigned = await AssignWorkStreamAsync(
                                issueId: dto.IssueId,
                                assigneeId: assignee.Id,
                                streamStatusId: assignee.StreamId,
                                threadId: 0,
                                targetDate: assignee.TargetDate ?? dto.TargetDate
                            );
                        }

                        var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                        return new PostWorkStreamResponse
                        {
                            WorkStreamId = lastAssigned!.StreamId,
                            ResourceId = lastAssigned.ResourceId ?? Guid.Empty,
                            StreamName = lastAssigned.StreamName ?? string.Empty,
                            StreamStatus = lastAssigned.StreamStatus,
                            StatusName = "New",
                            CompletionPct = 0,
                            ThreadCreated = false,
                            ThreadId = null,
                            TicketStatusId = ticketStatus.ComputedStatusId,
                            TicketStatusName = ticketStatus.ComputedStatusName,
                            TicketOverallPct = ticketStatus.OverallPct,
                            TotalSubtasks = ticketStatus.TotalSubtasks,
                            CompletedSubtasks = ticketStatus.CompletedSubtasks,
                            ActiveSubtasks = ticketStatus.ActiveSubtasks,
                            TicketCompleted = ticketStatus.TicketAutoCompleted,
                            IssueId = dto.IssueId,
                            RepoKey = ticketStatus.RepoKey,
                            IsTerminal = ticketStatus.IsTerminal,
                            BroadcastPayload = ticketStatus.BroadcastPayload,
                        };
                    }

                    // ── TYPE 2 / 3: Progress update ───────────────────────────────────

                    var resolvedStreamName = await ResolveStreamNameAsync(dto, posterId);
                    var resolvedStatus = dto.StreamStatus.HasValue
                        ? dto.StreamStatus.Value
                        : ResolveStreamStatus(dto.StreamName, dto.CompletionPct ?? 0);

                    // 1. Create the Thread
                    var (threadId, threadCreated) =
                        await HandleThreadAsync(dto, posterId, attachmentResult);

                    // 2. Validate Transition
                    await ValidateStatusTransitionAsync(resolvedStatus, posterId, dto.IssueId);

                    // 3. Upsert the CURRENT USER's stream row first.
                    //    We need stream.StreamId before the handoff logic below.
                    var stream = await UpsertStreamAsync(
                        dto, posterId, resolvedStatus, threadId, resolvedStreamName);

                    // ================================================================
                    // [HANDOFF LOGIC A] TESTER REPORTS A BUG
                    // ================================================================
                    if (dto.ReportTestFailure)
                    {
                        // Block the developer (existing logic — mutates tracked entities)
                        await HandleTestFailureAsync(dto, posterId);

                        // Find the Pending handoff where THIS tester is the target
                        var pendingHandoff = await _db.Set<WorkStreamHandoff>()
                            .Where(h => h.IssueId == dto.IssueId
                                     && h.TargetStreamId == stream.StreamId
                                     && h.Status == HandoffStatus.Pending)
                            .OrderByDescending(h => h.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (pendingHandoff != null)
                        {
                            pendingHandoff.Status = HandoffStatus.Failed;

                            // Link the bug thread to this failed handoff so the
                            // developer can see exactly which thread reported the bug
                            if (threadId > 0)
                            {
                                var bugThread = await _db.Set<ThreadMaster>()
                                    .FirstOrDefaultAsync(t => t.ThreadId == threadId);

                                if (bugThread != null)
                                    bugThread.FailedHandoffId = pendingHandoff.HandsOffId;
                            }

                            await _db.SaveChangesAsync();
                        }
                    }

                    // ================================================================
                    // [HANDOFF LOGIC B] TESTER PASSES THE CODE
                    // ================================================================
                    if (dto.ClearTestFailure)
                    {
                        // Unblock the developer + run count check (existing logic)
                        await HandleClearFailureAsync(dto, posterId);

                        // Mark the Pending handoff as Passed
                        var pendingHandoff = await _db.Set<WorkStreamHandoff>()
                            .Where(h => h.IssueId == dto.IssueId
                                     && h.TargetStreamId == stream.StreamId
                                     && h.Status == HandoffStatus.Pending)
                            .OrderByDescending(h => h.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (pendingHandoff != null)
                        {
                            pendingHandoff.Status = HandoffStatus.Passed;
                            pendingHandoff.UpdatedAt = DateTime.UtcNow;
                            pendingHandoff.UpdatedBy = posterId;
                            await _db.SaveChangesAsync();
                        }
                    }

                    // ================================================================
                    // [HANDOFF LOGIC C] DEVELOPER PUSHES CODE → assigns next person
                    // ================================================================
                    if (dto.NextAssignees != null && dto.NextAssignees.Any())
                    {
                        foreach (var assignee in dto.NextAssignees)
                        {
                            // 1. Assign the target (e.g. the Tester)
                            var targetStream = await AssignWorkStreamAsync(
                                issueId: dto.IssueId,
                                assigneeId: assignee.Id,
                                streamStatusId: assignee.StreamId,
                                threadId: threadId,
                                targetDate: assignee.TargetDate ?? dto.TargetDate
                            );
                            var seq = await _commonService.GetNextSequenceAsync("WorkStreamsHandsoff");
                            int SiNo = seq.CurrentValue;
                            // 2. Create the Handoff record
                            var newHandoff = new WorkStreamHandoff
                            {
                                HandsOffId = SiNo,
                                IssueId = dto.IssueId,
                                SourceStreamId = stream.StreamId,       // current user (Dev)
                                TargetStreamId = targetStream.StreamId, // next person (Tester)
                                InitiatingThreadId = threadId > 0 ? threadId : 0,
                                //HandoffType = $"{stream.StreamName}→{targetStream.StreamName}",
                                Status = HandoffStatus.Pending,
                                //SourceCompletionPctAtHandoff = (int?)(dto.CompletionPct ?? stream.CompletionPct ?? 0),
                                //SourceStreamStatusAtHandoff = resolvedStatus,
                                //CreatedAt = DateTime.UtcNow,
                                //CreatedBy = posterId,
                            };

                            _db.Set<WorkStreamHandoff>().Add(newHandoff);
                            await _db.SaveChangesAsync(); // must save now to get HandoffId

                            // 3. Map previously-failed handoffs that this push resolves
                            //    UI sends the HandoffIds it wants to close off
                            if (dto.ResolvedHandoffIds != null && dto.ResolvedHandoffIds.Any())
                            {
                                var bugsToResolve = await _db.Set<WorkStreamHandoff>()
                                    .Where(h => dto.ResolvedHandoffIds.Contains(h.HandsOffId))
                                    .ToListAsync();

                                foreach (var bug in bugsToResolve)
                                {
                                    bug.ResolvedByHandoffId = newHandoff.HandsOffId;
                                    bug.UpdatedAt = DateTime.UtcNow;
                                    bug.UpdatedBy = posterId;
                                }

                                await _db.SaveChangesAsync();
                            }
                        }
                    }

                    // Finally, compute the overall ticket status (unchanged)
                    var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                    return BuildResponse(dto, stream, resolvedStatus, threadId, threadCreated, ticketStatus2);
                });
            }
            catch (Exception ex) 
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(
                        attachmentResult.PermanentFilePathsCreated);
                throw;
            }
        }
        private async Task<string> ResolveStreamNameAsync(PostWorkStreamDto dto, Guid posterId)
        {
            // 1. UI explicitly sent a StreamName → use it directly
            if (!string.IsNullOrWhiteSpace(dto.StreamName))
                return dto.StreamName;

            // 2. UI sent a StreamStatus → resolve the stage label from Status_Master
            //    e.g. 5 → "In Development", 7 → "Unit Testing", 8 → "Functional Testing"
            //    This makes each stage uniquely identifiable — critical for multi-row tracking
            if (dto.StreamStatus.HasValue)
            {
                var statusName = await _db.StatusMasters
                    .Where(s => s.Status_Id == dto.StreamStatus.Value)
                    .Select(s => s.Status_Name)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(statusName))
                    return statusName;
            }

            // 3. Nothing provided → fall back to employee's department
            return await GetDepartmentNameAsync(posterId);
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
        private async Task<(long threadId, bool threadCreated)> HandleThreadAsync(
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
         long threadId,
         string resolvedStreamName)
        {
            // ── Priority: WorkStreamId sent → target that exact row ──────────────
            if (dto.WorkStreamId.HasValue)
            {
                var targetRow = await _db.WorkStreams
                    .FirstOrDefaultAsync(ws =>
                        ws.StreamId == dto.WorkStreamId.Value &&
                        ws.IssueId == dto.IssueId &&
                        ws.ResourceId == posterId);

                if (targetRow == null)
                    throw new InvalidOperationException("WorkStreamId not found.");

                // 🔥 ALLOW EXPLICIT RE-OPENING: Removed the downgrade exception
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == targetRow.StreamId,
                    ws =>
                    {
                        // Always update status and percentage on explicit action
                        ws.StreamStatus = resolvedStatus;
                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

                        if (threadId > 0)
                        {
                            ws.ThreadId = threadId;
                            if (ws.ParentThreadId == null) ws.ParentThreadId = threadId;
                        }
                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
                    }
                );
                return targetRow;
            }

            // ── Step 1: find ACTIVE row in the SAME STATUS FAMILY ─────────────────
            // This is the key fix: match by family, not by exact StreamName.
            //
            // Example:
            //   Dannu has row: StreamName="General", StreamStatus=5 (InDevelopment)
            //   Dannu posts:   StreamStatus=6 (DevelopmentCompleted)
            //   5 and 6 are both in DevFamily → FOUND → UPDATE existing row
            //   → NO new row created, StreamName stays "General"
            var familyRow = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
                .ToListAsync(); // load into memory for family check

            var sameFamilyRow = familyRow
                .FirstOrDefault(ws => StatusId.SameFamily(ws.StreamStatus!.Value, resolvedStatus));

            if (sameFamilyRow != null)
            {
                // SAME FAMILY: update the existing row in-place
                // e.g. InDevelopment(5) → DevelopmentCompleted(6), or InDev(5) → InDev(5) with new %
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == sameFamilyRow.StreamId,
                    ws =>
                    {
                        ws.StreamStatus = resolvedStatus;
                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

                        if (threadId > 0)
                        {
                            ws.ThreadId = threadId;
                            if (ws.ParentThreadId == null) ws.ParentThreadId = threadId;
                        }
                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
                    }
                );
                return sameFamilyRow;
            }

            // ── Step 2: check for already COMPLETED row in same family ────────────
            // User already completed this stage — thread-link only, no status change
            var completedFamilyRows = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
                .ToListAsync();

            var completedFamilyRow = completedFamilyRows
                .FirstOrDefault(ws => StatusId.SameFamily(ws.StreamStatus!.Value, resolvedStatus));

            if (completedFamilyRow != null)
            {
                // 🔥 ALLOW EXPLICIT RE-OPENING: Removed the downgrade exception
                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    ws => ws.StreamId == completedFamilyRow.StreamId,
                    ws =>
                    {
                        ws.StreamStatus = resolvedStatus;
                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

                        if (threadId > 0) ws.ThreadId = threadId;
                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
                    }
                );
                return completedFamilyRow;
            }

            // ── Step 3: self-move — moving to a DIFFERENT family ──────────────────
            // e.g. Dev (family: 5/6) → UnitTesting (family: 7/8/9/11)
            // Find their most advanced active row from a DIFFERENT family
            // and auto-complete it IF the new stage is more advanced
            //var allActiveRows = await _db.WorkStreams
            //    .Where(ws =>
            //        ws.IssueId == dto.IssueId &&
            //        ws.ResourceId == posterId &&
            //        ws.StreamStatus != null &&
            //        ws.StreamStatus != StatusId.Inactive &&
            //        ws.StreamStatus != StatusId.Cancelled &&
            //        !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
            //    .Join(_db.StatusMasters,
            //        ws => ws.StreamStatus,
            //        sm => sm.Status_Id,
            //        (ws, sm) => new { ws, sm.Sort_Order })
            //    .ToListAsync();

            //// Only consider rows NOT in the same family as resolvedStatus
            //var differentFamilyActiveRow = allActiveRows
            //    .Where(x => !StatusId.SameFamily(x.ws.StreamStatus!.Value, resolvedStatus))
            //    .OrderByDescending(x => x.Sort_Order)
            //    .Select(x => x.ws)
            //    .FirstOrDefault();

            //if (differentFamilyActiveRow != null)
            //{
            //    var prevSortOrder = allActiveRows
            //        .FirstOrDefault(x => x.ws.StreamId == differentFamilyActiveRow.StreamId)
            //        ?.Sort_Order ?? 0;

            //    var newSortOrder = await _db.StatusMasters
            //        .Where(s => s.Status_Id == resolvedStatus)
            //        .Select(s => s.Sort_Order)
            //        .FirstOrDefaultAsync();

            //    // Only auto-complete if genuinely moving forward
            //    if (newSortOrder > prevSortOrder)
            //    {
            //        // Determine correct completion status based on FAMILY, not StreamName
            //        var prevFamily = StatusId.GetFamily(differentFamilyActiveRow.StreamStatus!.Value);
            //        int completionStatus = prevFamily != null && prevFamily.Contains(StatusId.DevelopmentCompleted)
            //            ? StatusId.DevelopmentCompleted    // Dev family → DevCompleted
            //            : StatusId.FunctionalFixCompleted; // Test/other family → FuncFixCompleted

            //        await _domainService.UpdateTrackedEntityAsync<WorkStream>(
            //            ws => ws.StreamId == differentFamilyActiveRow.StreamId,
            //            ws =>
            //            {
            //                ws.StreamStatus = completionStatus;
            //                ws.CompletionPct = 100;
            //            }
            //        );
            //    }
            //}

            // ── Step 4: INSERT new row for the new stage ──────────────────────────
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
         int? streamStatusId,
         long? threadId,
         DateTime? targetDate)
        {
            // Resolve StreamName from Status_Master — MUST be uncommented
            string finalStreamName;
            finalStreamName = await GetDepartmentNameAsync(assigneeId);

            // Idempotent: same person + same stage + not completed = return existing
            var existing = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == issueId &&
                    ws.ResourceId == assigneeId &&
                    ws.StreamStatus == streamStatusId &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus ?? 0));

            if (existing != null)
                return existing;

            var newRow = new WorkStream
            {
                IssueId = issueId,
                StreamName = finalStreamName,
                ResourceId = assigneeId,
                StreamStatus = streamStatusId,
                CompletionPct = 0,
                TargetDate = targetDate,
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = threadId > 0 ? threadId : null,
            };

            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
            await EnsureTicketAssignedAsync(issueId);
            return newRow;
        }

        // =====================================================================
        // COMPUTE AND UPDATE TICKET STATUS
        //
        // Pure domain: updates Ticket.Status + Ticket.CompletionPct in DB
        // Returns TicketStatusResult with RepoKey + BroadcastPayload
        // The CALLER (WorkStreamRepo) does the actual SignalR broadcast
        // =====================================================================
        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid? issueId)
        {
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
            //var allCompleted = completedSubtasks == totalSubtasks;

            int computedStatusId;
            string computedStatusName;

            bool ownerExplicitlyClosed = subtasks.Any(s => s.StreamStatus == StatusId.Closed);
            bool allCompleted = false; // Default to false to prevent auto-completion flags

            if (ownerExplicitlyClosed)
            {
                computedStatusId = StatusId.Closed;  // 14
                computedStatusName = "Closed";
                overallPct = 100;
                allCompleted = true;
            }

            //if (allCompleted)
            //{
            //    computedStatusId = StatusId.Closed;  // 14
            //    computedStatusName = "Closed";
            //}
            else
            {
                if (overallPct > 90) overallPct = 90;
                // Most advanced ACTIVE stage = highest Sort_Order among non-completed
                var mostAdvanced = subtasks
                    .Where(s => !s.IsCompleted)
                    .OrderByDescending(s => s.Sort_Order)
                    .FirstOrDefault();
                if (mostAdvanced == null)
                {
                    mostAdvanced = subtasks.OrderByDescending(s => s.Sort_Order).First();
                }
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
            bool shouldReopen = isTerminal && activeSubtasks > 0 && !ownerExplicitlyClosed;

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
            long threadId,
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

        #region BULK UPSERT — TicketRepo (multiple assignees)
        // =====================================================================
        public async Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx)
        {
            var streamName = await GetDepartmentNameAsync(ctx.ResourceId);

            // ── Resolve StreamStatus if caller didn't specify one ─────────────────
            // Called from ticket CREATE or UPDATE where no explicit status is passed
            // → derive initial status from the employee's department
            var resolvedStatus = ctx.StreamStatus
                ?? ResolveStreamStatusFromDepartment(streamName);
            //   ↑ null = use dept logic
            //   set   = use what caller sent (e.g. StatusId.New for fresh assignment)

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
                        ws.StreamStatus = resolvedStatus;    // ← was ctx.StreamStatus
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
                    StreamStatus = resolvedStatus,           // ← was ctx.StreamStatus
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
                    StreamStatus = resolvedStatus,          // ← was ctx.StreamStatus
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
                    StreamStatus = resolvedStatus,            // ← was ctx.StreamStatus
                    WasInserted = true,
                    TicketStatus = ticketStatus2,
                };
            }
        }
        #endregion

        private static int ResolveStreamStatusFromDepartment(string departmentName)
        {
            var dept = (departmentName ?? string.Empty).ToUpperInvariant().Trim();

            // Developer teams → In Development
            if (dept.Contains("App_Devlopment") || dept.Contains("2") ||
                dept.Contains("SAP_Devlopment") || dept.Contains("3") ||
                dept.Contains("PROGRAMMER") || dept.Contains("ENGINEER") ||
                dept.Contains("CODING"))
                return StatusId.InDevelopment;        // 5

            // Functional / Business Analyst teams → Functional Testing stage
            if (dept.Contains("Functional") || dept.Contains("1") ||
                dept.Contains("CONSULTANT") || dept.Contains("BUSINESS ANALYST") ||
                dept.Contains("BA ") || dept == "BA")
                return StatusId.FunctionalFixCompleted;    // 8

            // QA / Testing teams → Functional Testing stage
            if (dept.Contains("QA") || dept.Contains("QUALITY") ||
                dept.Contains("TEST") || dept.Contains("TESTER"))
                return StatusId.FunctionalTesting;    // 8

            // UAT / Client teams → UAT Testing stage
            if (dept.Contains("UAT") || dept.Contains("CLIENT") ||
                dept.Contains("USER ACCEPT"))
                return StatusId.UATTesting;           // 9

            // Unknown department → safe default, not InDevelopment or Closed
            return StatusId.New;                      // 1
        }


        #region CLEAR ALL — ResourceIds = [] on ticket update
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
        #endregion

        #region MARK INACTIVE — specific people removed from ticket
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
        #endregion
    }
}


