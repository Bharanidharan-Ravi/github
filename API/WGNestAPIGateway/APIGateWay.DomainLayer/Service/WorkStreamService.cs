using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;

namespace APIGateWay.BusinessLayer.Repository
{
    public class WorkStreamService : IWorkStreamService
    {
        private readonly APIGatewayDBContext _db;
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContext;
        private readonly APIGateWayCommonService _commonService;
        private readonly IAttachmentService _attachmentService;
        private readonly IRequestStepContext _stepContext;            // ← ADDED

        public WorkStreamService(
            APIGatewayDBContext db,
            IDomainService domainService,
            ILoginContextService loginContext,
            APIGateWayCommonService commonService,
            IAttachmentService attachmentService,
            IRequestStepContext stepContext)                          // ← ADDED
        {
            _db = db;
            _domainService = domainService;
            _loginContext = loginContext;
            _commonService = commonService;
            _attachmentService = attachmentService;
            _stepContext = stepContext;                         // ← ADDED
        }

        // =====================================================================
        // POST WORKSTREAM — main public entry point
        // =====================================================================
        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var posterId = dto.ResourceId ?? _loginContext.userId;

                    // =========================================================================
                    // ── TICKET OVERALL PROGRESS (MANUAL UPDATE) ──────────────────────────────
                    // =========================================================================
                    if (dto.TicketOverallPercentage.HasValue || !string.IsNullOrWhiteSpace(dto.TicketStatusSummary))
                    {
                        var progTimer = _stepContext.StartStep();
                        try
                        {
                            // 1. Fetch the currently active log(s)
                            var activeLogs = await _db.Set<TicketProgressLog>()
             .Where(log => log.Issue_Id == dto.IssueId && log.IsActive && log.Assignee_Id == posterId)
             .ToListAsync();
                            var currentLog = activeLogs.FirstOrDefault();
                            decimal newPercentage = dto.TicketOverallPercentage ?? currentLog?.Percentage ?? 0;
                            string actionType = "";
                            var indiaTimeZone =
                                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                            var indiaTime =
                                TimeZoneInfo.ConvertTimeFromUtc(
                                    DateTime.UtcNow,
                                    indiaTimeZone
                                );
                            // 2. Logic: Insert New Row OR Update Existing Row
                            if (!string.IsNullOrWhiteSpace(dto.TicketStatusSummary) || currentLog == null)
                            {
                                // CREATE NEW ROW: Summary was provided, or no log exists yet
                                foreach (var log in activeLogs)
                                {
                                    log.IsActive = false;
                                }

                                var newLog = new TicketProgressLog
                                {
                                    LogId = Guid.NewGuid(), // Ensure LogId is generated
                                    Issue_Id = dto.IssueId,
                                    Assignee_Id = posterId,
                                    Percentage = newPercentage,
                                    StatusSummary = dto.TicketStatusSummary,
                                    IsActive = true,
                                    CreatedAt = indiaTime
                                };

                                await _db.Set<TicketProgressLog>().AddAsync(newLog);
                                actionType = "INSERT";
                            }
                            else
                            {
                                // UPDATE EXISTING ROW: Only Percentage was provided
                                currentLog.Percentage = newPercentage;

                                // Optional: Update who changed the percentage last
                                currentLog.Assignee_Id = posterId;
                                actionType = "UPDATE";
                            }

                            // 3. Update main TicketMaster for fast list fetching
                            var ticketMaster = await _db.Set<TicketMaster>().FindAsync(dto.IssueId);
                            if (ticketMaster != null)
                            {
                                ticketMaster.OverallPercentage = newPercentage;
                            }

                            await _db.SaveChangesAsync();

                            _stepContext.Success("TicketProgressLog", actionType, dto.IssueId.ToString(), progTimer);

                            // 4. SHORT-CIRCUIT: If the UI specifies this is ONLY a progress update,
                            // we return immediately and skip all the WorkStream logic below.
                            if (dto.IsTicketProgressOnly)
                            {
                                var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

                                return new PostWorkStreamResponse
                                {
                                    IssueId = dto.IssueId,
                                    TicketOverallPct = newPercentage, // Use the new manual percentage
                                    TicketStatusId = ticketStatus.ComputedStatusId,
                                    TicketStatusName = ticketStatus.ComputedStatusName,
                                    TotalSubtasks = ticketStatus.TotalSubtasks,
                                    CompletedSubtasks = ticketStatus.CompletedSubtasks,
                                    ActiveSubtasks = ticketStatus.ActiveSubtasks,
                                    TicketCompleted = ticketStatus.TicketAutoCompleted,
                                    RepoKey = ticketStatus.RepoKey,
                                    IsTerminal = ticketStatus.IsTerminal,
                                    BroadcastPayload = ticketStatus.BroadcastPayload,
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("TicketProgressLog", "UPSERT", ex.Message, ex.InnerException?.Message, progTimer);
                            throw;
                        }
                    }
                    // =========================================================================

                    // ── TYPE 1: Pure assignment ───────────────────────────────
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
                                targetDate: assignee.TargetDate ?? dto.TargetDate);
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

                    // ── TYPE 2/3: Progress update ─────────────────────────────
                    int? finalStreamName = await GetDepartmentNameAsync(posterId);
                    int targetStatusId = dto.StreamStatus ?? 0;

                    if (targetStatusId == 0)
                    {
                        var finalStream = finalStreamName.ToString();
                        if (!string.IsNullOrWhiteSpace(finalStream) &&
                            (finalStream.Contains("1") ||
                             finalStream.Contains("Functional", StringComparison.OrdinalIgnoreCase)))
                            targetStatusId = StatusId.FunctionalSupport;
                        else
                            targetStatusId = StatusId.InDevelopment;
                    }

                    var resolvedStreamName = await ResolveStreamNameAsync(dto, posterId);

                    WorkStreamHandoff handoffToUpdate = null;
                    if (dto.handsoffId.HasValue && dto.handsoffId.Value > 0)
                    {
                        handoffToUpdate = await _db.Set<WorkStreamHandoff>()
                            .FirstOrDefaultAsync(h => h.HandsOffId == dto.handsoffId.Value);
                    }
                    else
                    {
                        handoffToUpdate = await _db.Set<WorkStreamHandoff>()
                            .Where(h =>
                                h.IssueId == dto.IssueId &&
                                h.TargetStreamId == dto.WorkStreamId &&
                                h.Status == HandoffStatus.Pending)
                            .OrderByDescending(h => h.CreatedAt)
                            .FirstOrDefaultAsync();
                    }

                    // ── Thread ────────────────────────────────────────────────
                    var (threadId, threadCreated) =
                        await HandleThreadAsync(dto, posterId, handoffToUpdate?.HandsOffId, attachmentResult);

                    int? activeHandoffId = handoffToUpdate?.HandsOffId;
                    await ValidateStatusTransitionAsync(targetStatusId, posterId, dto.IssueId);

                    WorkStream stream = null;
                    bool isTerminalAction = targetStatusId == 14 || targetStatusId == 15;

                    if (!isTerminalAction)
                    {
                        // ── WorkStream upsert ─────────────────────────────────
                        stream = await UpsertStreamAsync(
                            dto, posterId, targetStatusId, threadId, resolvedStreamName);

                        if (handoffToUpdate != null)
                        {
                            // ── WorkStreamHandoff update ──────────────────────
                            var timer = _stepContext.StartStep();
                            try
                            {
                                handoffToUpdate.CompletionPct = dto.CompletionPct;
                                handoffToUpdate.UpdatedAt = DateTime.UtcNow;
                                handoffToUpdate.UpdatedBy = posterId;

                                if (dto.ClearTestFailure)
                                {
                                    handoffToUpdate.Status = HandoffStatus.Passed;
                                    handoffToUpdate.CompletionPct = 100;
                                }
                                else if (dto.ReportTestFailure)
                                    handoffToUpdate.Status = HandoffStatus.Failed;

                                await _db.SaveChangesAsync();

                                _stepContext.Success("WorkStreamHandoff", "UPDATE",
                                    handoffToUpdate.HandsOffId.ToString(), timer);
                            }
                            catch (Exception ex)
                            {
                                _stepContext.Failure("WorkStreamHandoff", "UPDATE",
                                    ex.Message, ex.InnerException?.Message, timer);
                                throw;
                            }

                            // Recalculate parent workstream percentage from all handoffs
                            var allMyHandoffs = await _db.Set<WorkStreamHandoff>()
                                .Where(h => h.TargetStreamId == stream.StreamId)
                                .ToListAsync();

                            if (allMyHandoffs.Any())
                            {
                                var avgPct = allMyHandoffs.Average(h => h.CompletionPct ?? 0);
                                stream.CompletionPct = Math.Round((decimal)avgPct, 2);

                                // ── WorkStream recalculated pct update ────────
                                var pctTimer = _stepContext.StartStep();
                                try
                                {
                                  await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                                        ws => ws.StreamId == stream.StreamId,
                                        ws => { ws.CompletionPct = stream.CompletionPct; });

                                    _stepContext.Success("WorkStream", "UPDATE",
                                        stream.StreamId.ToString(), pctTimer);
                                }
                                catch (Exception ex)
                                {
                                    _stepContext.Failure("WorkStream", "UPDATE",
                                        ex.Message, ex.InnerException?.Message, pctTimer);
                                    throw;
                                }
                            }
                        }

                        if (dto.NextAssignees != null && dto.NextAssignees.Any())
                        {
                            foreach (var assignee in dto.NextAssignees)
                            {
                                var targetStream = await AssignWorkStreamAsync(
                                    issueId: dto.IssueId,
                                    assigneeId: assignee.Id,
                                    streamStatusId: assignee.StreamId,
                                    threadId: threadId,
                                    targetDate: assignee.TargetDate ?? dto.TargetDate);

                                var seq = await _commonService.GetNextSequenceAsync("WorkStreamsHandsoff");
                                int siNo = seq.CurrentValue;

                                // ── WorkStreamHandoff INSERT ──────────────────
                                var timer = _stepContext.StartStep();
                                try
                                {
                                    var newHandoff = new WorkStreamHandoff
                                    {
                                        HandsOffId = siNo,
                                        IssueId = dto.IssueId,
                                        SourceStreamId = stream.StreamId,
                                        TargetStreamId = targetStream.StreamId,
                                        InitiatingThreadId = threadId > 0 ? threadId : 0,
                                        Status = HandoffStatus.Pending,
                                    };

                                    _db.Set<WorkStreamHandoff>().Add(newHandoff);
                                    await _db.SaveChangesAsync();

                                    if (activeHandoffId == null) activeHandoffId = newHandoff.HandsOffId;

                                    _stepContext.Success("WorkStreamHandoff", "INSERT",
                                        newHandoff.HandsOffId.ToString(), timer);
                                }
                                catch (Exception ex)
                                {
                                    _stepContext.Failure("WorkStreamHandoff", "INSERT",
                                        ex.Message, ex.InnerException?.Message, timer);
                                    throw;
                                }

                                if (dto.ResolvedHandoffIds != null && dto.ResolvedHandoffIds.Any())
                                {
                                    var bugsToResolve = await _db.Set<WorkStreamHandoff>()
                                        .Where(h => dto.ResolvedHandoffIds.Contains(h.HandsOffId))
                                        .ToListAsync();

                                    foreach (var bug in bugsToResolve)
                                    {
                                        bug.ResolvedByHandoffId = siNo;
                                        bug.UpdatedAt = DateTime.UtcNow;
                                        bug.UpdatedBy = posterId;
                                    }
                                    await _db.SaveChangesAsync();
                                }
                            }
                        }

                        if (threadId > 0 && stream?.StreamId != Guid.Empty)
                        {
                            // ── ThreadMaster back-link update ─────────────────
                            var timer = _stepContext.StartStep();
                            try
                            {
                               await _domainService.UpdateTrackedEntityAsync<ThreadMaster>(
                                    t => t.ThreadId == threadId,
                                    t =>
                                    {
                                        t.WorkStreamId = stream.StreamId;
                                        if (activeHandoffId.HasValue)
                                            t.HandsOffId = activeHandoffId.Value;
                                    });

                                _stepContext.Success("ThreadMaster", "UPDATE",
                                    threadId.ToString(), timer);
                            }
                            catch (Exception ex)
                            {
                                _stepContext.Failure("ThreadMaster", "UPDATE",
                                    ex.Message, ex.InnerException?.Message, timer);
                                throw;
                            }
                        }
                    }
                    else
                    {
                        // ── TERMINAL FLOW — force all active subtasks ─────────
                        stream = new WorkStream
                        {
                            StreamId = Guid.Empty,
                            ResourceId = posterId,
                            StreamName = targetStatusId == 14 ? "Ticket Closed" : "Ticket Cancelled",
                            StreamStatus = targetStatusId,
                            CompletionPct = targetStatusId == 14 ? 100 : 0
                        };

                        var activeSubtasks = await _db.WorkStreams
                            .Where(ws =>
                                ws.IssueId == dto.IssueId &&
                                ws.StreamStatus != StatusId.Inactive)
                            .ToListAsync();

                        var terminalTimer = _stepContext.StartStep();
                        try
                        {
                            foreach (var subtask in activeSubtasks)
                            {
                                subtask.StreamStatus = targetStatusId;
                                if (targetStatusId == 14) subtask.CompletionPct = 100;
                            }
                            await _db.SaveChangesAsync();

                            _stepContext.Success("WorkStream", "UPDATE(Terminal)",
                                $"{activeSubtasks.Count} rows forced to {targetStatusId}", terminalTimer);
                        }
                        catch (Exception ex)
                        {
                            _stepContext.Failure("WorkStream", "UPDATE(Terminal)",
                                ex.Message, ex.InnerException?.Message, terminalTimer);
                            throw;
                        }
                    }

                    var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(
                          dto.IssueId,
                          isTerminalAction ? targetStatusId : null,
                          dto.IsReopenRequest, // Pass the flag from UI
                          dto.IsReopenRequest ? posterId : null // Pass the user ID if reopening
                      );

                    return BuildResponse(dto, stream, targetStatusId, threadId, threadCreated, ticketStatus2);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                throw;
            }
        }

        // =====================================================================
        // HANDLE THREAD — creates ThreadMaster row if comment provided
        // =====================================================================
        private async Task<(long threadId, bool threadCreated)> HandleThreadAsync(
            PostWorkStreamDto dto,
            Guid posterId,
            int? HandsoffId,
            ProcessedAttachmentResult? attachmentResult)
        {
            if (dto.UseLastThread == true)
            {
                var last = await _db.ISSUETHREADS
                    .Where(t => t.Issue_Id == dto.IssueId && t.CreatedBy == posterId)
                    .OrderByDescending(t => t.ThreadId)
                    .FirstOrDefaultAsync();

                if (last == null)
                    throw new InvalidOperationException(
                        "No previous thread found. Disable the toggle and add a comment.");

                return (last.ThreadId, false);
            }

            if (!string.IsNullOrWhiteSpace(dto.CommentText))
            {
                var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
                var threadId = seq.CurrentValue;
                string finalHtml = dto.CommentText;

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                {
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var permFolder = $"{threadId}-{dto.IssueId}";
                    var relativePath = $"{permUserId}/{permFolder}";

                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        dto.CommentText, dto.temp.temps, relativePath,
                        threadId.ToString(), "ThreadMaster");

                    finalHtml = attachmentResult.UpdatedHtml;
                }

                var thread = new ThreadMaster
                {
                    ThreadId = threadId,
                    Issue_Id = dto.IssueId,
                    HtmlDesc = finalHtml,
                    HandsOffId = HandsoffId,
                    CommentText = HtmlUtilities.ConvertToPlainText(finalHtml),
                    CompletionPct = dto.CompletionPct,
                    From_Time = dto.From_Time,
                    To_Time = dto.To_Time,
                    Hours = dto.Hours,
                };

                // ── ThreadMaster INSERT ───────────────────────────────────────
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.SaveEntityWithAttachmentsAsync(
                        thread, attachmentResult?.Attachments);

                    _stepContext.Success("ThreadMaster", "INSERT", threadId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("ThreadMaster", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                    await _attachmentService.CleanupTempFiles(dto.temp);

                return (threadId, true);
            }

            return (0, false);
        }

        // =====================================================================
        // VALIDATE STATUS TRANSITION
        // =====================================================================
        private async Task ValidateStatusTransitionAsync(int resolvedStatus, Guid posterId, Guid? issueId)
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
        // UPSERT STREAM — insert or update poster's WorkStream row
        // =====================================================================
        private async Task<WorkStream> UpsertStreamAsync(
            PostWorkStreamDto dto,
            Guid posterId,
            int resolvedStatus,
            long threadId,
            string resolvedStreamName)
        {
            if (dto.WorkStreamId.HasValue)
            {
                var targetRow = await _db.WorkStreams
                    .FirstOrDefaultAsync(ws =>
                        ws.StreamId == dto.WorkStreamId.Value &&
                        ws.IssueId == dto.IssueId &&
                        ws.ResourceId == posterId);

                if (targetRow == null)
                    throw new InvalidOperationException("WorkStreamId not found.");

                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                        ws => ws.StreamId == targetRow.StreamId,
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
                        });

                    _stepContext.Success("WorkStream", "UPDATE", targetRow.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                return targetRow;
            }

            // Find active row in same status family
            var familyRow = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == dto.IssueId &&
                    ws.ResourceId == posterId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
                .ToListAsync();

            var sameFamilyRow = familyRow
                .FirstOrDefault(ws => StatusId.SameFamily(ws.StreamStatus!.Value, resolvedStatus));

            if (sameFamilyRow != null)
            {
                var timer = _stepContext.StartStep();
                try
                {
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
                        });

                    _stepContext.Success("WorkStream", "UPDATE", sameFamilyRow.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                return sameFamilyRow;
            }

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
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                        ws => ws.StreamId == completedFamilyRow.StreamId,
                        ws =>
                        {
                            ws.StreamStatus = resolvedStatus;
                            ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;
                            if (threadId > 0) ws.ThreadId = threadId;
                            if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
                        });

                    _stepContext.Success("WorkStream", "UPDATE", completedFamilyRow.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                return completedFamilyRow;
            }

            // INSERT new row
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

            var insertTimer = _stepContext.StartStep();
            try
            {
                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                await EnsureTicketAssignedAsync(dto.IssueId);

                _stepContext.Success("WorkStream", "INSERT", newRow.StreamId.ToString(), insertTimer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("WorkStream", "INSERT",
                    ex.Message, ex.InnerException?.Message, insertTimer);
                throw;
            }

            return newRow;
        }

        // =====================================================================
        // ASSIGN WORKSTREAM — creates row for a person without any thread
        // =====================================================================
        public async Task<WorkStream> AssignWorkStreamAsync(
            Guid issueId, Guid assigneeId, int? streamStatusId, long? threadId, DateTime? targetDate)
        {
            int? departmentId = await GetDepartmentNameAsync(assigneeId);
            string finalStreamName = departmentId?.ToString();
            int? targetStatusId = streamStatusId;

            if (targetStatusId == 0)
            {
                if (!string.IsNullOrWhiteSpace(finalStreamName) &&
                    (finalStreamName.Contains("1") ||
                     finalStreamName.Contains("Functional", StringComparison.OrdinalIgnoreCase)))
                    targetStatusId = StatusId.FunctionalSupport;
                else
                    targetStatusId = StatusId.InDevelopment;
            }

            var existing = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == issueId &&
                    ws.ResourceId == assigneeId &&
                    ws.StreamStatus == targetStatusId &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled &&
                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus ?? 0));

            if (existing != null) return existing;

            var newRow = new WorkStream
            {
                IssueId = issueId,
                StreamName = finalStreamName,
                ResourceId = assigneeId,
                StreamStatus = targetStatusId,
                CompletionPct = 0,
                TargetDate = targetDate,
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = threadId > 0 ? threadId : null,
            };

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                await EnsureTicketAssignedAsync(issueId);

                _stepContext.Success("WorkStream", "INSERT", newRow.StreamId.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("WorkStream", "INSERT",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            return newRow;
        }

        // =====================================================================
        // COMPUTE AND UPDATE TICKET STATUS
        // =====================================================================
        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(
       Guid? issueId, int? forceTerminalStatusId = null, bool isReopenRequest = false, Guid? reopenedBy = null)
        {
            var subtasks = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != StatusId.Inactive)
                .Join(_db.StatusMasters,
                    ws => ws.StreamStatus ?? StatusId.New,
                    sm => sm.Status_Id,
                    (ws, sm) => new
                    {
                        ws.StreamStatus,
                        ws.CompletionPct,
                        sm.Sort_Order,
                        sm.Status_Name,
                        IsCompleted = ws.StreamStatus.HasValue &&
                                      StatusId.CompletedStatuses.Contains(ws.StreamStatus.Value)
                    })
                .ToListAsync();

            if (!subtasks.Any() && forceTerminalStatusId == null)
            {
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
            }

            var overallPct = subtasks.Any()
                ? Math.Round(subtasks.Average(s => (double)(s.CompletionPct ?? 0)), 2)
                : 0;

            var totalSubtasks = subtasks.Count;
            var completedSubtasks = subtasks.Count(s => s.IsCompleted);
            var activeSubtasks = subtasks.Count(s => !s.IsCompleted);

            int computedStatusId;
            string computedStatusName;

            bool isExplicitlyClosed = forceTerminalStatusId == StatusId.Closed || subtasks.Any(s => s.StreamStatus == StatusId.Closed);
            bool isExplicitlyCancelled = forceTerminalStatusId == StatusId.Cancelled || subtasks.Any(s => s.StreamStatus == StatusId.Cancelled);

            if (isExplicitlyClosed)
            {
                computedStatusId = StatusId.Closed;
                computedStatusName = "Closed";
                overallPct = 100;
            }
            else if (isExplicitlyCancelled)
            {
                computedStatusId = StatusId.Cancelled;
                computedStatusName = "Cancelled";
            }
            else
            {
                if (overallPct > 90) overallPct = 90;

                var mostAdvanced = subtasks
                    .Where(s => !s.IsCompleted)
                    .OrderByDescending(s => s.Sort_Order)
                    .FirstOrDefault();

                if (mostAdvanced == null && subtasks.Any())
                    mostAdvanced = subtasks.OrderByDescending(s => s.Sort_Order).First();

                computedStatusId = mostAdvanced?.StreamStatus ?? StatusId.New;
                computedStatusName = mostAdvanced?.Status_Name ?? "New";
            }

            var ticket = await _db.Set<TicketMaster>().FirstOrDefaultAsync(t => t.Issue_Id == issueId);
            // 🔥 NEW: TEAM-BASED REOPEN STATUS OVERRIDE 🔥
            if (isReopenRequest && ticket != null)
            {
                // 1. Get the ticket owner's (Assignee's) Team ID
                // (This uses your existing GetDepartmentNameAsync which returns the team int)
                int? ownerTeamId = await GetDepartmentNameAsync(ticket.Assignee_Id);

                // 2. Map the Status based on Team ID
                if (ownerTeamId == 1) // Functional Team
                {
                    computedStatusId = 11; // FunctionalSupport
                    computedStatusName = "Functional Support";
                }
                else // Technical (2) or Web (3)
                {
                    computedStatusId = 5; // InDevelopment
                    computedStatusName = "In Development";
                }

                // Optional: Reset ticket progress to 0% when reopened
                overallPct = 0;
            }
            bool isTerminal = computedStatusId == StatusId.Closed || computedStatusId == StatusId.Cancelled;
            bool shouldReopen = isTerminal && activeSubtasks > 0;

            if (ticket != null)
            {
                // ── TicketMaster UPDATE ───────────────────────────────────────
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                        t => t.Issue_Id == issueId,
                        t =>
                        {
                            t.Status = computedStatusId;
                            t.CompletionPct = (decimal?)overallPct;
                            t.StatusName = computedStatusName;
                            if (isReopenRequest)
                            {
                                t.ReopenCount += 1;
                                t.ReopenedBy = reopenedBy;
                            }
                        });

                    _stepContext.Success("TicketMaster", "UPDATE(StatusCompute)",
                        issueId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("TicketMaster", "UPDATE(StatusCompute)",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }
            }

            if (shouldReopen) isTerminal = false;

            string repoKey = string.Empty;
            try
            {
                if (ticket?.RepoId != null)
                    repoKey = await _db.RepositoryMasters
                        .Where(r => r.Repo_Id == ticket.RepoId)
                        .Select(r => r.RepoKey)
                        .FirstOrDefaultAsync() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkStreamService] RepoKey lookup failed for {issueId}: {ex.Message}");
            }

            var broadcastPayload = isTerminal ? null : (object)new
            {
                Issue_Id = issueId,
                Status = computedStatusId,
                StatusName = computedStatusName,
                OverallPct = overallPct,
                TotalSubtasks = totalSubtasks,
                CompletedSubtasks = completedSubtasks,
                ActiveSubtasks = activeSubtasks,
                AutoClosed = false,
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
                TicketAutoCompleted = false,
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
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                        ws => ws.StreamId == existing.StreamId,
                        ws =>
                        {
                            ws.StreamStatus = ctx.StreamStatus;
                            ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

                            if (ctx.ParentThreadId.HasValue && ws.ParentThreadId == null)
                                ws.ParentThreadId = ctx.ParentThreadId;

                            if (ctx.TargetDate.HasValue) ws.TargetDate = ctx.TargetDate;
                        });

                    _stepContext.Success("WorkStream", "UPDATE", existing.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

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
                    StreamName = streamName.ToString(),
                    ResourceId = ctx.ResourceId,
                    StreamStatus = ctx.StreamStatus,
                    CompletionPct = ctx.CompletionPct ?? 0,
                    TargetDate = ctx.TargetDate,
                    ParentThreadId = ctx.ParentThreadId,
                };

                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                    await EnsureTicketAssignedAsync(ctx.IssueId);

                    _stepContext.Success("WorkStream", "INSERT", newRow.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

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
        // BULK UPSERT — called from TicketRepo (multiple assignees)
        // =====================================================================
        public async Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx)
        {
            int? streamName = await GetDepartmentNameAsync(ctx.ResourceId);
            var stream = streamName.ToString();
            var resolvedStatus = ctx.StreamStatus ?? ResolveStreamStatusFromDepartment(stream);

            var existing = await _db.WorkStreams
                .FirstOrDefaultAsync(ws =>
                    ws.IssueId == ctx.IssueId &&
                    ws.ResourceId == ctx.ResourceId &&
                    ws.StreamStatus != null &&
                    ws.StreamStatus != StatusId.Inactive &&
                    ws.StreamStatus != StatusId.Cancelled);

            if (existing != null)
            {
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                        ws => ws.StreamId == existing.StreamId,
                        ws =>
                        {
                            ws.StreamStatus = resolvedStatus;
                            ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

                            if (ctx.ParentThreadId.HasValue && ws.ParentThreadId == null)
                                ws.ParentThreadId = ctx.ParentThreadId;

                            if (ctx.TargetDate.HasValue) ws.TargetDate = ctx.TargetDate;
                        });

                    _stepContext.Success("WorkStream", "UPDATE", existing.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "UPDATE",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                var ticketStatus1 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = existing.StreamId,
                    StreamName = existing.StreamName,
                    ResourceId = existing.ResourceId!.Value,
                    StreamStatus = resolvedStatus,
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
                    StreamName = streamName.ToString(),
                    ResourceId = ctx.ResourceId,
                    StreamStatus = resolvedStatus,
                    CompletionPct = ctx.CompletionPct ?? 0,
                    TargetDate = ctx.TargetDate,
                    ParentThreadId = ctx.ParentThreadId,
                };

                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
                    await EnsureTicketAssignedAsync(ctx.IssueId);

                    _stepContext.Success("WorkStream", "INSERT", newRow.StreamId.ToString(), timer);
                }
                catch (Exception ex)
                {
                    _stepContext.Failure("WorkStream", "INSERT",
                        ex.Message, ex.InnerException?.Message, timer);
                    throw;
                }

                var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

                return new WorkStreamResult
                {
                    StreamId = newRow.StreamId,
                    StreamName = newRow.StreamName,
                    ResourceId = newRow.ResourceId!.Value,
                    StreamStatus = resolvedStatus,
                    WasInserted = true,
                    TicketStatus = ticketStatus2,
                };
            }
        }

        // =====================================================================
        // CLEAR ALL
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

            var timer = _stepContext.StartStep();
            try
            {
                foreach (var row in rows) row.StreamStatus = StatusId.Inactive;
                await _db.SaveChangesAsync();

                _stepContext.Success("WorkStream", "UPDATE(ClearAll)",
                    $"{rows.Count} rows set Inactive", timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("WorkStream", "UPDATE(ClearAll)",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }
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

            var timer = _stepContext.StartStep();
            try
            {
                foreach (var row in rows) row.StreamStatus = StatusId.Inactive;
                await _db.SaveChangesAsync();

                _stepContext.Success("WorkStream", "UPDATE(MarkInactive)",
                    string.Join(",", removedResourceIds), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("WorkStream", "UPDATE(MarkInactive)",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

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
        // PRIVATE HELPERS
        // =====================================================================
        private async Task EnsureTicketAssignedAsync(Guid? issueId)
        {
            await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                t => t.Issue_Id == issueId,
                t => { if (t.Status == StatusId.New) t.Status = StatusId.Assigned; });
        }

        public async Task<int?> GetDepartmentNameAsync(Guid? resourceId)
        {
            var emp = await _db.eMPLOYEEMASTERs
                .Where(e => e.EmployeeID == resourceId)
                .Select(e => new { e.Team })
                .FirstOrDefaultAsync();
            return emp?.Team;
        }

        private async Task<string> ResolveStreamNameAsync(PostWorkStreamDto dto, Guid posterId)
        {
            if (!string.IsNullOrWhiteSpace(dto.StreamName)) return dto.StreamName;

            if (dto.StreamStatus.HasValue)
            {
                var statusName = await _db.StatusMasters
                    .Where(s => s.Status_Id == dto.StreamStatus.Value)
                    .Select(s => s.Status_Name)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(statusName)) return statusName;
            }

            int? departmentId = await GetDepartmentNameAsync(posterId);
            return departmentId?.ToString();
        }

        private static int ResolveStreamStatus(string streamName, decimal completionPct)
        {
            var name = (streamName ?? string.Empty).ToUpperInvariant();

            bool isDeveloper = name.Contains("DEV") || name.Contains("DEVELOP");
            bool isTester = name.Contains("TEST") || name.Contains("QA") ||
                               name.Contains("FUNCTIONAL") || name.Contains("QUALITY");

            if (isDeveloper)
                return completionPct >= 100 ? StatusId.DevelopmentCompleted : StatusId.InDevelopment;

            if (isTester)
                return completionPct >= 100 ? StatusId.FunctionalFixCompleted : StatusId.FunctionalTesting;

            return completionPct >= 100 ? StatusId.Closed : StatusId.InDevelopment;
        }

        private static int ResolveStreamStatusFromDepartment(string departmentName)
        {
            var dept = (departmentName ?? string.Empty).ToUpperInvariant().Trim();

            if (dept.Contains("App_Devlopment") || dept.Contains("2") ||
                dept.Contains("SAP_Devlopment") || dept.Contains("3") ||
                dept.Contains("PROGRAMMER") || dept.Contains("ENGINEER") ||
                dept.Contains("CODING"))
                return StatusId.InDevelopment;

            if (dept.Contains("Functional") || dept.Contains("1") ||
                dept.Contains("CONSULTANT") || dept.Contains("BUSINESS ANALYST") ||
                dept.Contains("BA ") || dept == "BA")
                return StatusId.FunctionalFixCompleted;

            if (dept.Contains("QA") || dept.Contains("QUALITY") ||
                dept.Contains("TEST") || dept.Contains("TESTER"))
                return StatusId.FunctionalTesting;

            if (dept.Contains("UAT") || dept.Contains("CLIENT") ||
                dept.Contains("USER ACCEPT"))
                return StatusId.UATTesting;

            return StatusId.New;
        }

        private static PostWorkStreamResponse BuildResponse(
            PostWorkStreamDto dto, WorkStream? stream, int resolvedStatus,
            long threadId, bool threadCreated, TicketStatusResult ticketStatus)
        {
            return new PostWorkStreamResponse
            {
                WorkStreamId = stream.StreamId,
                ResourceId = stream.ResourceId ?? Guid.Empty,
                StreamName = stream.StreamName ?? dto.StreamName,
                StreamStatus = resolvedStatus,
                CompletionPct = dto.CompletionPct ?? stream.CompletionPct ?? 0,
                IsBlocked = stream.BlockedByTestFailure,
                BlockedReason = stream.BlockedReason,
                ThreadId = threadId > 0 ? threadId : null,
                ParentThreadId = stream.ParentThreadId,
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
                        ? "Developer unblocked — can now mark development completed."
                        : null,
                IssueId = dto.IssueId,
                RepoKey = ticketStatus.RepoKey,
                IsTerminal = ticketStatus.IsTerminal,
                BroadcastPayload = ticketStatus.BroadcastPayload,
            };
        }
    }
}









#region WithouLogs 
//using APIGateWay.DomainLayer.CommonSevice;
//using APIGateWay.DomainLayer.DBContext;
//using APIGateWay.DomainLayer.Helpers;
//using APIGateWay.DomainLayer.Interface;
//using APIGateWay.ModalLayer.MasterData;
//using APIGateWay.ModalLayer.PostData;
//using Microsoft.EntityFrameworkCore;
//using System.Threading;

//namespace APIGateWay.BusinessLayer.Repository
//{
//    // =========================================================================
//    // WorkStreamService — pure domain logic, NO SignalR
//    //
//    // Architecture:
//    //   Service  → DB writes, status compute, RepoKey lookup, payload build
//    //   Repo     → calls service, then does SignalR broadcasts
//    //   Controller → validation only, delegates to Repo
//    //
//    // PK naming matches your entity: StreamId (Guid, IDENTITY)
//    // StatusMaster PK: Status_Id
//    // StatusId values from your statusmasterEntity.cs:
//    //   New=1, Assigned=2, InDevelopment=5, DevelopmentCompleted=6,
//    //   UnitTesting=7, FunctionalTesting=8, UATTesting=9,
//    //   FunctionalFixCompleted=11, MovedToProduction=12,
//    //   OnHold=13, Closed=14, Cancelled=15, Inactive=16
//    // =========================================================================

//    public class WorkStreamService : IWorkStreamService
//    {
//        private readonly APIGatewayDBContext _db;
//        private readonly IDomainService _domainService;
//        private readonly ILoginContextService _loginContext;
//        private readonly APIGateWayCommonService _commonService;
//        private readonly IAttachmentService _attachmentService;
//        private readonly IApiLoggerService _apiLogger;

//        public WorkStreamService(
//            APIGatewayDBContext db,
//            IDomainService domainService,
//            ILoginContextService loginContext,
//            APIGateWayCommonService commonService,
//            IAttachmentService attachmentService,
//            IApiLoggerService apiLogger)
//        {
//            _db = db;
//            _domainService = domainService;
//            _loginContext = loginContext;
//            _commonService = commonService;
//            _attachmentService = attachmentService;
//            _apiLogger = apiLogger;
//        }

//        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
//        {
//            ProcessedAttachmentResult attachmentResult = null;

//            try
//            {
//                return await _domainService.ExecuteInTransactionAsync(async () =>
//                {
//                    var posterId = dto.ResourceId ?? _loginContext.userId;

//                    // ── TYPE 1: Pure assignment — AssignOnly=true ─────────────────────
//                    if (dto.AssignOnly)
//                    {
//                        if (dto.NextAssignees == null || !dto.NextAssignees.Any())
//                            throw new InvalidOperationException(
//                                "NextAssignees is required when AssignOnly is true.");

//                        WorkStream lastAssigned = null;
//                        foreach (var assignee in dto.NextAssignees)
//                        {
//                            lastAssigned = await AssignWorkStreamAsync(
//                                issueId: dto.IssueId,
//                                assigneeId: assignee.Id,
//                                streamStatusId: assignee.StreamId,
//                                threadId: 0,
//                                targetDate: assignee.TargetDate ?? dto.TargetDate
//                            );
//                        }

//                        var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

//                        return new PostWorkStreamResponse
//                        {
//                            WorkStreamId = lastAssigned!.StreamId,
//                            ResourceId = lastAssigned.ResourceId ?? Guid.Empty,
//                            StreamName = lastAssigned.StreamName ?? string.Empty,
//                            StreamStatus = lastAssigned.StreamStatus,
//                            StatusName = "New",
//                            CompletionPct = 0,
//                            ThreadCreated = false,
//                            ThreadId = null,
//                            TicketStatusId = ticketStatus.ComputedStatusId,
//                            TicketStatusName = ticketStatus.ComputedStatusName,
//                            TicketOverallPct = ticketStatus.OverallPct,
//                            TotalSubtasks = ticketStatus.TotalSubtasks,
//                            CompletedSubtasks = ticketStatus.CompletedSubtasks,
//                            ActiveSubtasks = ticketStatus.ActiveSubtasks,
//                            TicketCompleted = ticketStatus.TicketAutoCompleted,
//                            IssueId = dto.IssueId,
//                            RepoKey = ticketStatus.RepoKey,
//                            IsTerminal = ticketStatus.IsTerminal,
//                            BroadcastPayload = ticketStatus.BroadcastPayload,
//                        };
//                    }

//                    // ── TYPE 2 / 3: Progress update ───────────────────────────────────

//                    int? finalStreamName = await GetDepartmentNameAsync(posterId);
//                    int targetStatusId = dto.StreamStatus ?? 0; // Handle nulls safely

//                    if (targetStatusId == 0)
//                    {
//                        var finalStream = finalStreamName.ToString();
//                        // If the department is "1" (or contains "1" / "Functional")
//                        if (!string.IsNullOrWhiteSpace(finalStream) &&
//                           (finalStream.Contains("1") || finalStream.Contains("Functional", StringComparison.OrdinalIgnoreCase)))
//                        {
//                            targetStatusId = StatusId.FunctionalSupport; // 11
//                        }
//                        else
//                        {
//                            // Fallback if it's 0 but the department is NOT 1 (Default to New/Assigned)
//                            targetStatusId = StatusId.InDevelopment; // 1
//                        }
//                    }

//                    var resolvedStreamName = await ResolveStreamNameAsync(dto, posterId);
//                    //var resolvedStatus = dto.StreamStatus.HasValue
//                    //    ? dto.StreamStatus.Value
//                    //    : ResolveStreamStatus(dto.StreamName, dto.CompletionPct ?? 0);
//                    WorkStreamHandoff handoffToUpdate = null;

//                    // 1. If the frontend passed a specific handoffId, use it!
//                    if (dto.handsoffId.HasValue && dto.handsoffId.Value > 0)
//                    {
//                        handoffToUpdate = await _db.Set<WorkStreamHandoff>()
//                            .FirstOrDefaultAsync(h => h.HandsOffId == dto.handsoffId.Value);
//                    }
//                    else
//                    {
//                        // 2. FALLBACK: Automatically find their most recent Pending handoff!
//                        handoffToUpdate = await _db.Set<WorkStreamHandoff>()
//                            .Where(h => h.IssueId == dto.IssueId
//                                     && h.TargetStreamId == dto.WorkStreamId
//                                     && h.Status == HandoffStatus.Pending)
//                            .OrderByDescending(h => h.CreatedAt)
//                            .FirstOrDefaultAsync();
//                    }
//                    // 1. Create the Thread
//                    var (threadId, threadCreated) =
//                        await HandleThreadAsync(dto,  posterId, handoffToUpdate?.HandsOffId, attachmentResult);

//                    int? activeHandoffId = handoffToUpdate?.HandsOffId;
//                    // 2. Validate Transition
//                    await ValidateStatusTransitionAsync(targetStatusId, posterId, dto.IssueId);
//                    // 3. Upsert the CURRENT USER's stream row first.
//                    //    We need stream.StreamId before the handoff logic below.
//                    WorkStream stream = null;
//                    bool isTerminalAction = (targetStatusId == 14 || targetStatusId == 15);

//                    if (!isTerminalAction)
//                    {
//                        // ================================================================
//                        // NORMAL FLOW (Developers & Testers)
//                        // ================================================================
//                        stream = await UpsertStreamAsync(
//                            dto, posterId, targetStatusId, threadId, resolvedStreamName);

//                        //if (dto.handsoffId.HasValue && dto.handsoffId.Value > 0)
//                        //{
//                        //    var handoffToUpdate = await _db.Set<WorkStreamHandoff>()
//                        //        .FirstOrDefaultAsync(h => h.HandsOffId == dto.handsoffId.Value);

//                            if (handoffToUpdate != null)
//                            {
//                                // 1. Save the percentage to this specific Handoff
//                                handoffToUpdate.CompletionPct = dto.CompletionPct;
//                                handoffToUpdate.UpdatedAt = DateTime.UtcNow;
//                                handoffToUpdate.UpdatedBy = posterId;

//                                // Force 100% if they explicitly clicked the "Pass" button
//                                if (dto.ClearTestFailure)
//                                {
//                                    handoffToUpdate.Status = HandoffStatus.Passed;
//                                    handoffToUpdate.CompletionPct = 100;
//                                }
//                                // Mark as failed if they clicked "Report Bug"
//                                else if (dto.ReportTestFailure)
//                                {
//                                    handoffToUpdate.Status = HandoffStatus.Failed;
//                                }

//                                await _db.SaveChangesAsync();

//                                // 2. 🔥 RECALCULATE THE PARENT WORKSTREAM PERCENTAGE 🔥
//                                // Grab ALL handoffs assigned to this Tester to find the true average
//                                var allMyHandoffs = await _db.Set<WorkStreamHandoff>()
//                                    .Where(h => h.TargetStreamId == stream.StreamId)
//                                    .ToListAsync();

//                                if (allMyHandoffs.Any())
//                                {
//                                    // Calculate the average! (e.g., 50 + 0 / 2 = 25%)
//                                    var avgPct = allMyHandoffs.Average(h => h.CompletionPct ?? 0);
//                                    stream.CompletionPct = Math.Round((decimal)avgPct, 2);

//                                    // Update the Tester's main progress bar with the new average
//                                    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                                        ws => ws.StreamId == stream.StreamId,
//                                        ws => { ws.CompletionPct = stream.CompletionPct ; }
//                                    );
//                                }
//                            }
//                        //}

//                        if (dto.NextAssignees != null && dto.NextAssignees.Any())
//                        {
//                            foreach (var assignee in dto.NextAssignees)
//                            {
//                                // 1. Assign the target (e.g. the Tester)
//                                var targetStream = await AssignWorkStreamAsync(
//                                    issueId: dto.IssueId,
//                                    assigneeId: assignee.Id,
//                                    streamStatusId: assignee.StreamId,
//                                    threadId: threadId,
//                                    targetDate: assignee.TargetDate ?? dto.TargetDate
//                                );

//                                var seq = await _commonService.GetNextSequenceAsync("WorkStreamsHandsoff");
//                                int SiNo = seq.CurrentValue;

//                                // 2. Create the Handoff record
//                                var newHandoff = new WorkStreamHandoff
//                                {
//                                    HandsOffId = SiNo,
//                                    IssueId = dto.IssueId,
//                                    SourceStreamId = stream.StreamId,
//                                    TargetStreamId = targetStream.StreamId,
//                                    InitiatingThreadId = threadId > 0 ? threadId : 0,
//                                    Status = HandoffStatus.Pending,
//                                };

//                                _db.Set<WorkStreamHandoff>().Add(newHandoff);
//                                await _db.SaveChangesAsync(); // must save now to get HandoffId
//                                if (activeHandoffId == null)
//                                {
//                                    activeHandoffId = newHandoff.HandsOffId;
//                                }
//                                // 3. Map previously-failed handoffs that this push resolves
//                                if (dto.ResolvedHandoffIds != null && dto.ResolvedHandoffIds.Any())
//                                {
//                                    var bugsToResolve = await _db.Set<WorkStreamHandoff>()
//                                        .Where(h => dto.ResolvedHandoffIds.Contains(h.HandsOffId))
//                                        .ToListAsync();

//                                    foreach (var bug in bugsToResolve)
//                                    {
//                                        bug.ResolvedByHandoffId = newHandoff.HandsOffId;
//                                        bug.UpdatedAt = DateTime.UtcNow;
//                                        bug.UpdatedBy = posterId;
//                                    }
//                                    await _db.SaveChangesAsync();
//                                }
//                            }
//                        }
//                        // Now that the Stream and Handoffs exist, we link them backwards to the Thread!
//                        // ================================================================
//                        if (threadId > 0 && stream != null && stream.StreamId != Guid.Empty)
//                        {
//                            await _domainService.UpdateTrackedEntityAsync<ThreadMaster>(
//                                t => t.ThreadId == threadId,
//                                t =>
//                                {
//                                    t.WorkStreamId = stream.StreamId; // Must add WorkStreamId property to ThreadMaster model!

//                                    if (activeHandoffId.HasValue)
//                                    {
//                                        t.HandsOffId = activeHandoffId.Value;
//                                    }
//                                }
//                            );
//                        }
//                    }
//                    else
//                    {
//                        // ================================================================
//                        // TERMINAL FLOW (Owner Closes or Cancels Ticket)
//                        // ================================================================

//                        // 1. Create a "Dummy" stream so BuildResponse doesn't crash!
//                        stream = new WorkStream
//                        {
//                            StreamId = Guid.Empty, // Empty Guid because this isn't going into the DB
//                            ResourceId = posterId,
//                            StreamName = targetStatusId == 14 ? "Ticket Closed" : "Ticket Cancelled",
//                            StreamStatus = targetStatusId,
//                            CompletionPct = targetStatusId == 14 ? 100 : 0
//                        };

//                        // 2. CRITICAL: We MUST force all active subtasks to close/cancel.
//                        // If we don't, ComputeAndUpdateTicketStatusAsync will see active 
//                        // developers and instantly reopen the ticket.
//                        var activeSubtasks = await _db.WorkStreams
//                            .Where(ws => ws.IssueId == dto.IssueId && ws.StreamStatus != StatusId.Inactive)
//                            .ToListAsync();

//                        foreach (var subtask in activeSubtasks)
//                        {
//                            subtask.StreamStatus = targetStatusId; // Force to 14 or 15
//                            if (targetStatusId == 14) subtask.CompletionPct = 100;
//                        }

//                        await _db.SaveChangesAsync(); // Save the forced closures
//                    }
//                    // Finally, compute the overall ticket status (unchanged)
//                    var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(
//                         dto.IssueId,
//                         isTerminalAction ? targetStatusId : null
//                     );

//                    return BuildResponse(dto, stream, targetStatusId, threadId, threadCreated, ticketStatus2);
//                });
//            }
//            catch (Exception ex) 
//            {
//                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
//                    _attachmentService.RollbackPhysicalFiles(
//                        attachmentResult.PermanentFilePathsCreated);
//                throw;
//            }
//        }
//        private async Task<string> ResolveStreamNameAsync(PostWorkStreamDto dto, Guid posterId)
//        {
//            // 1. UI explicitly sent a StreamName → use it directly
//            if (!string.IsNullOrWhiteSpace(dto.StreamName))
//                return dto.StreamName;

//            // 2. UI sent a StreamStatus → resolve the stage label from Status_Master
//            //    e.g. 5 → "In Development", 7 → "Unit Testing", 8 → "Functional Testing"
//            //    This makes each stage uniquely identifiable — critical for multi-row tracking
//            if (dto.StreamStatus.HasValue)
//            {
//                var statusName = await _db.StatusMasters
//                    .Where(s => s.Status_Id == dto.StreamStatus.Value)
//                    .Select(s => s.Status_Name)
//                    .FirstOrDefaultAsync();

//                if (!string.IsNullOrWhiteSpace(statusName))
//                    return statusName;
//            }
//            int? departmentId = await GetDepartmentNameAsync(posterId);

//            // 2. Convert the integer ID to a string for the DB column
//            // If departmentId is null, it falls back to "General" (or whatever default you prefer)
//            string finalStreamName = departmentId?.ToString();
//            // 3. Nothing provided → fall back to employee's department
//            return finalStreamName;
//        }
//        // =====================================================================
//        // AUTO-RESOLVE StreamStatus from StreamName + CompletionPct
//        //
//        // Uses your StatusId values:
//        //   Developer: 100% → DevelopmentCompleted(6), <100% → InDevelopment(5)
//        //   Tester:    100% → FunctionalFixCompleted(11), <100% → FunctionalTesting(8)
//        //   Other:     100% → Closed(14), <100% → InDevelopment(5)
//        // =====================================================================
//        private static int ResolveStreamStatus(string streamName, decimal completionPct)
//        {
//            var name = (streamName ?? string.Empty).ToUpperInvariant();

//            bool isDeveloper = name.Contains("DEV") || name.Contains("DEVELOP");
//            bool isTester = name.Contains("TEST") || name.Contains("QA") ||
//                               name.Contains("FUNCTIONAL") || name.Contains("QUALITY");

//            if (isDeveloper)
//                return completionPct >= 100
//                    ? StatusId.DevelopmentCompleted   // 6
//                    : StatusId.InDevelopment;         // 5

//            if (isTester)
//                return completionPct >= 100
//                    ? StatusId.FunctionalFixCompleted // 11
//                    : StatusId.FunctionalTesting;     // 8

//            // Fallback
//            return completionPct >= 100
//                ? StatusId.Closed        // 14
//                : StatusId.InDevelopment; // 5
//        }

//        // =====================================================================
//        // 1. HANDLE THREAD
//        // Returns (threadId=0, false) when no thread needed (pure % update)
//        // =====================================================================
//        private async Task<(long threadId, bool threadCreated)> HandleThreadAsync(
//            PostWorkStreamDto dto,
//            Guid posterId,
//            int? HandsoffId,
//            ProcessedAttachmentResult? attachmentResult)
//        {
//            // Toggle ON: link the last thread this user posted
//            if (dto.UseLastThread == true)
//            {
//                var last = await _db.ISSUETHREADS
//                    .Where(t =>
//                        t.Issue_Id == dto.IssueId &&
//                        t.CreatedBy == posterId)
//                    .OrderByDescending(t => t.ThreadId)
//                    .FirstOrDefaultAsync();

//                if (last == null)
//                    throw new InvalidOperationException(
//                        "No previous thread found. Disable the toggle and add a comment.");

//                return (last.ThreadId, false);
//            }

//            // Comment provided: create new thread with optional attachments
//            if (!string.IsNullOrWhiteSpace(dto.CommentText))
//            {
//                var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
//                var threadId = seq.CurrentValue;
//                string finalHtml = dto.CommentText;

//                if (dto.temp?.temps != null && dto.temp.temps.Any())
//                {
//                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
//                    var permFolder = $"{threadId}-{dto.IssueId}";
//                    var relativePath = $"{permUserId}/{permFolder}";

//                    attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
//                        dto.CommentText, dto.temp.temps, relativePath,
//                        threadId.ToString(), "ThreadMaster");

//                    finalHtml = attachmentResult.UpdatedHtml;
//                }

//                var thread = new ThreadMaster
//                {
//                    ThreadId = threadId,
//                    Issue_Id = dto.IssueId,
//                    HtmlDesc = finalHtml,
//                    HandsOffId = HandsoffId,
//                    CommentText = HtmlUtilities.ConvertToPlainText(finalHtml),
//                    CompletionPct = dto.CompletionPct,  
//                    From_Time = dto.From_Time,   // null = not logged
//                    To_Time = dto.To_Time,
//                    Hours = dto.Hours,
//                };

//                await _domainService.SaveEntityWithAttachmentsAsync(
//                    thread, attachmentResult?.Attachments);

//                if (dto.temp?.temps != null && dto.temp.temps.Any())
//                    await _attachmentService.CleanupTempFiles(dto.temp);

//                return (threadId, true);
//            }

//            // Neither: pure % update — no thread at all
//            return (0, false);
//        }

//        // =====================================================================
//        // 3. VALIDATE STATUS TRANSITION
//        // Blocks DevCompleted when test failure is open
//        // =====================================================================
//        private async Task ValidateStatusTransitionAsync(
//            int resolvedStatus, Guid posterId, Guid? issueId)
//        {
//            if (resolvedStatus != StatusId.DevelopmentCompleted) return;

//            var row = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == issueId &&
//                    ws.ResourceId == posterId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive)
//                .FirstOrDefaultAsync();

//            if (row?.BlockedByTestFailure == true)
//                throw new InvalidOperationException(
//                    $"Cannot mark Development Completed. Testing failed: " +
//                    $"{row.BlockedReason ?? "bugs reported"}. " +
//                    "The tester must verify the fix and clear the failure flag first.");
//        }

//        // =====================================================================
//        // 4. UPSERT STREAM — insert or update poster's WorkStream row
//        // =====================================================================
//        private async Task<WorkStream> UpsertStreamAsync(
//         PostWorkStreamDto dto,
//         Guid posterId,
//         int resolvedStatus,
//         long threadId,
//         string resolvedStreamName)
//        {
//            // ── Priority: WorkStreamId sent → target that exact row ──────────────
//            if (dto.WorkStreamId.HasValue)
//            {
//                var targetRow = await _db.WorkStreams
//                    .FirstOrDefaultAsync(ws =>
//                        ws.StreamId == dto.WorkStreamId.Value &&
//                        ws.IssueId == dto.IssueId &&
//                        ws.ResourceId == posterId);

//                if (targetRow == null)
//                    throw new InvalidOperationException("WorkStreamId not found.");

//                // 🔥 ALLOW EXPLICIT RE-OPENING: Removed the downgrade exception
//                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                    ws => ws.StreamId == targetRow.StreamId,
//                    ws =>
//                    {
//                        // Always update status and percentage on explicit action
//                        ws.StreamStatus = resolvedStatus;
//                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

//                        if (threadId > 0)
//                        {
//                            ws.ThreadId = threadId;
//                            if (ws.ParentThreadId == null) ws.ParentThreadId = threadId;
//                        }
//                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
//                    }
//                );
//                return targetRow;
//            }

//            // ── Step 1: find ACTIVE row in the SAME STATUS FAMILY ─────────────────
//            // This is the key fix: match by family, not by exact StreamName.
//            //
//            // Example:
//            //   Dannu has row: StreamName="General", StreamStatus=5 (InDevelopment)
//            //   Dannu posts:   StreamStatus=6 (DevelopmentCompleted)
//            //   5 and 6 are both in DevFamily → FOUND → UPDATE existing row
//            //   → NO new row created, StreamName stays "General"
//            var familyRow = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == dto.IssueId &&
//                    ws.ResourceId == posterId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled &&
//                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
//                .ToListAsync(); // load into memory for family check

//            var sameFamilyRow = familyRow
//                .FirstOrDefault(ws => StatusId.SameFamily(ws.StreamStatus!.Value, resolvedStatus));

//            if (sameFamilyRow != null)
//            {
//                // SAME FAMILY: update the existing row in-place
//                // e.g. InDevelopment(5) → DevelopmentCompleted(6), or InDev(5) → InDev(5) with new %
//                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                    ws => ws.StreamId == sameFamilyRow.StreamId,
//                    ws =>
//                    {
//                        ws.StreamStatus = resolvedStatus;
//                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

//                        if (threadId > 0)
//                        {
//                            ws.ThreadId = threadId;
//                            if (ws.ParentThreadId == null) ws.ParentThreadId = threadId;
//                        }
//                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
//                    }
//                );
//                return sameFamilyRow;
//            }

//            // ── Step 2: check for already COMPLETED row in same family ────────────
//            // User already completed this stage — thread-link only, no status change
//            var completedFamilyRows = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == dto.IssueId &&
//                    ws.ResourceId == posterId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled &&
//                    StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value))
//                .ToListAsync();

//            var completedFamilyRow = completedFamilyRows
//                .FirstOrDefault(ws => StatusId.SameFamily(ws.StreamStatus!.Value, resolvedStatus));

//            if (completedFamilyRow != null)
//            {
//                // 🔥 ALLOW EXPLICIT RE-OPENING: Removed the downgrade exception
//                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                    ws => ws.StreamId == completedFamilyRow.StreamId,
//                    ws =>
//                    {
//                        ws.StreamStatus = resolvedStatus;
//                        ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;

//                        if (threadId > 0) ws.ThreadId = threadId;
//                        if (dto.TargetDate.HasValue) ws.TargetDate = dto.TargetDate;
//                    }
//                );
//                return completedFamilyRow;
//            }

//            // ── Step 4: INSERT new row for the new stage ──────────────────────────
//            var newRow = new WorkStream
//            {
//                IssueId = dto.IssueId,
//                StreamName = resolvedStreamName,
//                ResourceId = posterId,
//                StreamStatus = resolvedStatus,
//                CompletionPct = dto.CompletionPct ?? 0,
//                TargetDate = dto.TargetDate,
//                ThreadId = threadId > 0 ? threadId : null,
//                ParentThreadId = threadId > 0 ? threadId : null,
//            };

//            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
//            await EnsureTicketAssignedAsync(dto.IssueId);
//            return newRow;
//        }

//        // =====================================================================
//        // ASSIGN WORK STREAM — create row for a person without any thread
//        // Used for:
//        //   - AssignOnly=true (owner assigns directly)
//        //   - Developer 100% → pass to tester (NextAssigneeId)
//        //   - Reassigning after tester removed
//        // =====================================================================
//        public async Task<WorkStream> AssignWorkStreamAsync(
//         Guid issueId,
//         Guid assigneeId,
//         int? streamStatusId,
//         long? threadId,
//         DateTime? targetDate)
//        {
//            // Resolve StreamName from Status_Master — MUST be uncommented
//            int? departmentId = await GetDepartmentNameAsync(assigneeId);

//            // 2. Convert the integer ID to a string for the DB column
//            // If departmentId is null, it falls back to "General" (or whatever default you prefer)
//            string finalStreamName = departmentId?.ToString();
//            int? targetStatusId = streamStatusId; // Handle nulls safely

//            if (targetStatusId == 0)
//            {
//                // If the department is "1" (or contains "1" / "Functional")
//                if (!string.IsNullOrWhiteSpace(finalStreamName) &&
//                   (finalStreamName.Contains("1") || finalStreamName.Contains("Functional", StringComparison.OrdinalIgnoreCase)))
//                {
//                    targetStatusId = StatusId.FunctionalSupport; // 11
//                }
//                else
//                {
//                    // Fallback if it's 0 but the department is NOT 1 (Default to New/Assigned)
//                    targetStatusId = StatusId.InDevelopment; // 1
//                }
//            }

//            // Idempotent: same person + same stage + not completed = return existing
//            var existing = await _db.WorkStreams
//                .FirstOrDefaultAsync(ws =>
//                    ws.IssueId == issueId &&
//                    ws.ResourceId == assigneeId &&
//                    ws.StreamStatus == targetStatusId &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled &&
//                    !StatusId.CompletedStatuses.Contains(ws.StreamStatus ?? 0));

//            if (existing != null)
//                return existing;

//            var newRow = new WorkStream
//            {
//                IssueId = issueId,
//                StreamName = finalStreamName,
//                ResourceId = assigneeId,
//                StreamStatus = targetStatusId,
//                CompletionPct = 0,
//                TargetDate = targetDate,
//                ThreadId = threadId > 0 ? threadId : null,
//                ParentThreadId = threadId > 0 ? threadId : null,
//            };

//            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
//            await EnsureTicketAssignedAsync(issueId);
//            return newRow;
//        }

//        // =====================================================================
//        // COMPUTE AND UPDATE TICKET STATUS
//        //
//        // Pure domain: updates Ticket.Status + Ticket.CompletionPct in DB
//        // Returns TicketStatusResult with RepoKey + BroadcastPayload
//        // The CALLER (WorkStreamRepo) does the actual SignalR broadcast
//        // =====================================================================
//        // 🔥 Added the optional parameter here
//        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid? issueId, int? forceTerminalStatusId = null)
//        {
//            var subtasks = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == issueId &&
//                    ws.StreamStatus != StatusId.Inactive)
//                .Join(_db.StatusMasters,
//                    ws => ws.StreamStatus ?? StatusId.New,
//                    sm => sm.Status_Id,
//                    (ws, sm) => new
//                    {
//                        ws.StreamStatus,
//                        ws.CompletionPct,
//                        sm.Sort_Order,
//                        sm.Status_Name,
//                        IsCompleted = ws.StreamStatus.HasValue &&
//                                      StatusId.CompletedStatuses.Contains(ws.StreamStatus.Value)
//                    })
//                .ToListAsync();

//            // 🔥 If there are no subtasks AND the owner isn't trying to force it closed, return New.
//            if (!subtasks.Any() && forceTerminalStatusId == null)
//            {
//                return new TicketStatusResult
//                {
//                    ComputedStatusId = StatusId.New,
//                    ComputedStatusName = "New",
//                    OverallPct = 0,
//                    TotalSubtasks = 0,
//                    CompletedSubtasks = 0,
//                    ActiveSubtasks = 0,
//                    TicketAutoCompleted = false,
//                };
//            }

//            // 🔥 Safely handle the math! Only run Average() if the list actually has items.
//            var overallPct = subtasks.Any()
//                ? Math.Round(subtasks.Average(s => (double)(s.CompletionPct ?? 0)), 2)
//                : 0;

//            var totalSubtasks = subtasks.Count;
//            var completedSubtasks = subtasks.Count(s => s.IsCompleted);
//            var activeSubtasks = subtasks.Count(s => !s.IsCompleted);

//            int computedStatusId;
//            string computedStatusName;

//            // 🔥 Check both the database rows AND the new explicit parameter
//            bool isExplicitlyClosed = forceTerminalStatusId == StatusId.Closed || subtasks.Any(s => s.StreamStatus == StatusId.Closed);
//            bool isExplicitlyCancelled = forceTerminalStatusId == StatusId.Cancelled || subtasks.Any(s => s.StreamStatus == StatusId.Cancelled);

//            if (isExplicitlyClosed)
//            {
//                computedStatusId = StatusId.Closed;  // 14
//                computedStatusName = "Closed";
//                overallPct = 100; // Force to 100% on close
//            }
//            else if (isExplicitlyCancelled)
//            {
//                computedStatusId = StatusId.Cancelled; // 15
//                computedStatusName = "Cancelled";
//            }
//            else
//            {
//                if (overallPct > 90) overallPct = 90;

//                var mostAdvanced = subtasks
//                    .Where(s => !s.IsCompleted)
//                    .OrderByDescending(s => s.Sort_Order)
//                    .FirstOrDefault();

//                if (mostAdvanced == null && subtasks.Any())
//                {
//                    mostAdvanced = subtasks.OrderByDescending(s => s.Sort_Order).First();
//                }

//                // Fallback safely just in case MostAdvanced is null
//                computedStatusId = mostAdvanced?.StreamStatus ?? StatusId.New;
//                computedStatusName = mostAdvanced?.Status_Name ?? "New";
//            }

//            var ticket = await _db.Set<TicketMaster>()
//                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

//            bool isTerminal = computedStatusId == StatusId.Closed || computedStatusId == StatusId.Cancelled;

//            bool shouldReopen = isTerminal && activeSubtasks > 0;

//            if (ticket != null)
//            {
//                await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
//                    t => t.Issue_Id == issueId,
//                    t =>
//                    {
//                        t.Status = computedStatusId;
//                        t.CompletionPct = (decimal?)overallPct;
//                        t.StatusName = computedStatusName;
//                    }
//                );
//            }

//            if (shouldReopen)
//                isTerminal = false;

//            string repoKey = string.Empty;
//            try
//            {
//                if (ticket?.RepoId != null)
//                {
//                    repoKey = await _db.RepositoryMasters
//                        .Where(r => r.Repo_Id == ticket.RepoId)
//                        .Select(r => r.RepoKey)
//                        .FirstOrDefaultAsync() ?? string.Empty;
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[WorkStreamService] RepoKey lookup failed for {issueId}: {ex.Message}");
//            }

//            var broadcastPayload = isTerminal ? null : (object)new
//            {
//                Issue_Id = issueId,
//                Status = computedStatusId,
//                StatusName = computedStatusName,
//                OverallPct = overallPct,
//                TotalSubtasks = totalSubtasks,
//                CompletedSubtasks = completedSubtasks,
//                ActiveSubtasks = activeSubtasks,
//                AutoClosed = false,
//                UpdatedAt = DateTime.UtcNow,
//            };

//            return new TicketStatusResult
//            {
//                ComputedStatusId = computedStatusId,
//                ComputedStatusName = computedStatusName,
//                OverallPct = (decimal)overallPct,
//                TotalSubtasks = totalSubtasks,
//                CompletedSubtasks = completedSubtasks,
//                ActiveSubtasks = activeSubtasks,
//                TicketAutoCompleted = false,
//                RepoKey = repoKey,
//                IsTerminal = isTerminal,
//                BroadcastPayload = broadcastPayload,
//            };
//        }

//        // =====================================================================
//        // SINGLE UPSERT — called from ThreadRepo / TicketRepo
//        // =====================================================================
//        public async Task<WorkStreamResult> UpsertWorkStreamAsync(WorkStreamContext ctx)
//        {
//            var streamName = await GetDepartmentNameAsync(ctx.ResourceId);

//            var existing = await _db.WorkStreams
//                .FirstOrDefaultAsync(ws =>
//                    ws.IssueId == ctx.IssueId &&
//                    ws.ResourceId == ctx.ResourceId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled);

//            if (existing != null)
//            {
//                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                    ws => ws.StreamId == existing.StreamId,
//                    ws =>
//                    {
//                        ws.StreamStatus = ctx.StreamStatus;
//                        ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

//                        if (ctx.ParentThreadId.HasValue && ws.ParentThreadId == null)
//                            ws.ParentThreadId = ctx.ParentThreadId;

//                        if (ctx.TargetDate.HasValue)
//                            ws.TargetDate = ctx.TargetDate;
//                    }
//                );

//                var ticketStatus1 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

//                return new WorkStreamResult
//                {
//                    StreamId = existing.StreamId,
//                    StreamName = existing.StreamName,
//                    ResourceId = existing.ResourceId!.Value,
//                    StreamStatus = ctx.StreamStatus,
//                    WasInserted = false,
//                    IsBlocked = existing.BlockedByTestFailure,
//                    BlockedReason = existing.BlockedReason,
//                    TicketStatus = ticketStatus1,
//                };
//            }
//            else
//            {
//                var newRow = new WorkStream
//                {
//                    IssueId = ctx.IssueId,
//                    StreamName = streamName.ToString(),
//                    ResourceId = ctx.ResourceId,
//                    StreamStatus = ctx.StreamStatus,
//                    CompletionPct = ctx.CompletionPct ?? 0,
//                    TargetDate = ctx.TargetDate,
//                    ParentThreadId = ctx.ParentThreadId,
//                };

//                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
//                await EnsureTicketAssignedAsync(ctx.IssueId);

//                var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

//                return new WorkStreamResult
//                {
//                    StreamId = newRow.StreamId,
//                    StreamName = newRow.StreamName,
//                    ResourceId = newRow.ResourceId!.Value,
//                    StreamStatus = newRow.StreamStatus,
//                    WasInserted = true,
//                    TicketStatus = ticketStatus2,
//                };
//            }
//        }

//        // =====================================================================
//        // PRIVATE HELPERS
//        // =====================================================================
//        private async Task EnsureTicketAssignedAsync(Guid? issueId)
//        {
//            await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
//                t => t.Issue_Id == issueId,
//                t => { if (t.Status == StatusId.New) t.Status = StatusId.Assigned; }
//            );
//        }

//        public async Task<int?> GetDepartmentNameAsync(Guid? resourceId)
//        {
//            var emp = await _db.eMPLOYEEMASTERs
//                .Where(e => e.EmployeeID == resourceId)
//                .Select(e => new { e.Team }) // e.Team is already an int!
//                .FirstOrDefaultAsync();

//            // Just return it directly. 
//            // The '?.' operator naturally returns null if 'emp' wasn't found.
//            return emp?.Team;
//        }

//        private static PostWorkStreamResponse BuildResponse(
//            PostWorkStreamDto dto,
//            WorkStream? stream,
//            int resolvedStatus,
//            long threadId,
//            bool threadCreated,
//            TicketStatusResult ticketStatus)
//        {
//            return new PostWorkStreamResponse
//            {
//                // WorkStream subtask
//                WorkStreamId = stream.StreamId,
//                ResourceId = stream.ResourceId ?? Guid.Empty,
//                StreamName = stream.StreamName ?? dto.StreamName,
//                StreamStatus = resolvedStatus,
//                CompletionPct = dto.CompletionPct ?? stream.CompletionPct ?? 0,
//                IsBlocked = stream.BlockedByTestFailure,
//                BlockedReason = stream.BlockedReason,

//                // Thread
//                ThreadId = threadId > 0 ? threadId : null,
//                ParentThreadId = stream.ParentThreadId,
//                ThreadCreated = threadCreated,

//                // Ticket live status
//                TicketStatusId = ticketStatus.ComputedStatusId,
//                TicketStatusName = ticketStatus.ComputedStatusName,
//                TicketOverallPct = ticketStatus.OverallPct,
//                TotalSubtasks = ticketStatus.TotalSubtasks,
//                CompletedSubtasks = ticketStatus.CompletedSubtasks,
//                ActiveSubtasks = ticketStatus.ActiveSubtasks,
//                TicketCompleted = ticketStatus.TicketAutoCompleted,

//                // Test failure / unblock
//                DeveloperBlocked = dto.ReportTestFailure,
//                DeveloperUnblocked = dto.ClearTestFailure,
//                BlockSummary = dto.ReportTestFailure
//                    ? $"Developer blocked: {dto.TestFailureComment}"
//                    : dto.ClearTestFailure
//                        ? "Developer unblocked — can now mark development completed."
//                        : null,

//                // Broadcast data (used by WorkStreamRepo, not sent to UI)
//                IssueId = dto.IssueId,
//                RepoKey = ticketStatus.RepoKey,
//                IsTerminal = ticketStatus.IsTerminal,
//                BroadcastPayload = ticketStatus.BroadcastPayload,
//            };
//        }

//        #region BULK UPSERT — TicketRepo (multiple assignees)
//        // =====================================================================
//        public async Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx)
//        {
//            int? streamName = await GetDepartmentNameAsync(ctx.ResourceId);
//            var stream = streamName.ToString();
//            // ── Resolve StreamStatus if caller didn't specify one ─────────────────
//            // Called from ticket CREATE or UPDATE where no explicit status is passed
//            // → derive initial status from the employee's department
//            var resolvedStatus = ctx.StreamStatus
//                ?? ResolveStreamStatusFromDepartment(stream);
//            //   ↑ null = use dept logic
//            //   set   = use what caller sent (e.g. StatusId.New for fresh assignment)

//            var existing = await _db.WorkStreams
//                .FirstOrDefaultAsync(ws =>
//                    ws.IssueId == ctx.IssueId &&
//                    ws.ResourceId == ctx.ResourceId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled);

//            if (existing != null)
//            {
//                await _domainService.UpdateTrackedEntityAsync<WorkStream>(
//                    ws => ws.StreamId == existing.StreamId,
//                    ws =>
//                    {
//                        ws.StreamStatus = resolvedStatus;    // ← was ctx.StreamStatus
//                        ws.CompletionPct = ctx.CompletionPct ?? ws.CompletionPct;

//                        if (ctx.ParentThreadId.HasValue && ws.ParentThreadId == null)
//                            ws.ParentThreadId = ctx.ParentThreadId;

//                        if (ctx.TargetDate.HasValue)
//                            ws.TargetDate = ctx.TargetDate;
//                    }
//                );

//                var ticketStatus1 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

//                return new WorkStreamResult
//                {
//                    StreamId = existing.StreamId,
//                    StreamName = existing.StreamName,
//                    ResourceId = existing.ResourceId!.Value,
//                    StreamStatus = resolvedStatus,           // ← was ctx.StreamStatus
//                    WasInserted = false,
//                    IsBlocked = existing.BlockedByTestFailure,
//                    BlockedReason = existing.BlockedReason,
//                    TicketStatus = ticketStatus1,
//                };
//            }
//            else
//            {
//                var newRow = new WorkStream
//                {
//                    IssueId = ctx.IssueId,
//                    StreamName = streamName.ToString(),
//                    ResourceId = ctx.ResourceId,
//                    StreamStatus = resolvedStatus,          // ← was ctx.StreamStatus
//                    CompletionPct = ctx.CompletionPct ?? 0,
//                    TargetDate = ctx.TargetDate,
//                    ParentThreadId = ctx.ParentThreadId,
//                };

//                await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
//                await EnsureTicketAssignedAsync(ctx.IssueId);

//                var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId);

//                return new WorkStreamResult
//                {
//                    StreamId = newRow.StreamId,
//                    StreamName = newRow.StreamName,
//                    ResourceId = newRow.ResourceId!.Value,
//                    StreamStatus = resolvedStatus,            // ← was ctx.StreamStatus
//                    WasInserted = true,
//                    TicketStatus = ticketStatus2,
//                };
//            }
//        }
//        #endregion

//        private static int ResolveStreamStatusFromDepartment(string departmentName)
//        {
//            var dept = (departmentName ?? string.Empty).ToUpperInvariant().Trim();

//            // Developer teams → In Development
//            if (dept.Contains("App_Devlopment") || dept.Contains("2") ||
//                dept.Contains("SAP_Devlopment") || dept.Contains("3") ||
//                dept.Contains("PROGRAMMER") || dept.Contains("ENGINEER") ||
//                dept.Contains("CODING"))
//                return StatusId.InDevelopment;        // 5

//            // Functional / Business Analyst teams → Functional Testing stage
//            if (dept.Contains("Functional") || dept.Contains("1") ||
//                dept.Contains("CONSULTANT") || dept.Contains("BUSINESS ANALYST") ||
//                dept.Contains("BA ") || dept == "BA")
//                return StatusId.FunctionalFixCompleted;    // 8

//            // QA / Testing teams → Functional Testing stage
//            if (dept.Contains("QA") || dept.Contains("QUALITY") ||
//                dept.Contains("TEST") || dept.Contains("TESTER"))
//                return StatusId.FunctionalTesting;    // 8

//            // UAT / Client teams → UAT Testing stage
//            if (dept.Contains("UAT") || dept.Contains("CLIENT") ||
//                dept.Contains("USER ACCEPT"))
//                return StatusId.UATTesting;           // 9

//            // Unknown department → safe default, not InDevelopment or Closed
//            return StatusId.New;                      // 1
//        }


//        #region CLEAR ALL — ResourceIds = [] on ticket update
//        // =====================================================================
//        public async Task ClearWorkStreamsAsync(Guid issueId)
//        {
//            var rows = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == issueId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    ws.StreamStatus != StatusId.Cancelled)
//                .ToListAsync();

//            if (!rows.Any()) return;

//            foreach (var row in rows)
//                row.StreamStatus = StatusId.Inactive;

//            await _db.SaveChangesAsync();
//        }
//        #endregion

//        #region MARK INACTIVE — specific people removed from ticket
//        // =====================================================================
//        public async Task MarkInactiveAsync(Guid issueId, List<Guid> removedResourceIds)
//        {
//            if (!removedResourceIds.Any()) return;

//            var rows = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == issueId &&
//                    ws.StreamStatus != null &&
//                    ws.StreamStatus != StatusId.Inactive &&
//                    removedResourceIds.Contains(ws.ResourceId!.Value))
//                .ToListAsync();

//            if (!rows.Any()) return;

//            foreach (var row in rows)
//                row.StreamStatus = StatusId.Inactive;

//            await _db.SaveChangesAsync();

//            // Auto-clear blocks that were set by any removed tester
//            // Prevents permanent block if tester was removed before clearing
//            var devBlocksToRelease = await _db.WorkStreams
//                .Where(ws =>
//                    ws.IssueId == issueId &&
//                    ws.BlockedByTestFailure == true &&
//                    ws.BlockedByResourceId != null &&
//                    removedResourceIds.Contains(ws.BlockedByResourceId!.Value))
//                .ToListAsync();

//            foreach (var row in devBlocksToRelease)
//            {
//                row.BlockedByTestFailure = false;
//                row.BlockedReason = null;
//                row.BlockedAt = null;
//                row.BlockedByResourceId = null;
//            }

//            if (devBlocksToRelease.Any())
//                await _db.SaveChangesAsync();
//        }
//        #endregion
//    }
//}
#endregion

#region PUBLIC ENTRY POINT — reads like a table of contents
// =====================================================================
//public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
//{
//    ProcessedAttachmentResult attachmentResult = null;

//    try
//    {
//        return await _domainService.ExecuteInTransactionAsync(async () =>
//        {
//            // Poster is always the logged-in user
//            var posterId = dto.ResourceId ?? _loginContext.userId;

//            // ── TYPE 1: Pure assignment — no thread, no % update ──────
//            if (dto.AssignOnly)
//            {
//                if (!dto.NextAssigneeId.HasValue)
//                    throw new InvalidOperationException(
//                        "NextAssigneeId is required when AssignOnly is true.");

//                var assigned = await AssignWorkStreamAsync(
//                    issueId: dto.IssueId,
//                    assigneeId: dto.NextAssigneeId.Value,
//                    streamStatusId: dto.NextAssigneeStreamId,
//                    targetDate: dto.TargetDate
//                );

//                var ticketStatusForAssign =
//                    await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

//                return new PostWorkStreamResponse
//                {
//                    WorkStreamId = assigned.StreamId,
//                    ResourceId = assigned.ResourceId ?? Guid.Empty,
//                    StreamName = assigned.StreamName ?? string.Empty,
//                    StreamStatus = assigned.StreamStatus,
//                    StatusName = "New",
//                    CompletionPct = 0,
//                    ThreadCreated = false,
//                    ThreadId = null,
//                    TicketStatusId = ticketStatusForAssign.ComputedStatusId,
//                    TicketStatusName = ticketStatusForAssign.ComputedStatusName,
//                    TicketOverallPct = ticketStatusForAssign.OverallPct,
//                    TotalSubtasks = ticketStatusForAssign.TotalSubtasks,
//                    CompletedSubtasks = ticketStatusForAssign.CompletedSubtasks,
//                    ActiveSubtasks = ticketStatusForAssign.ActiveSubtasks,
//                    TicketCompleted = ticketStatusForAssign.TicketAutoCompleted,
//                    IssueId = dto.IssueId,
//                    RepoKey = ticketStatusForAssign.RepoKey,
//                    IsTerminal = ticketStatusForAssign.IsTerminal,
//                    BroadcastPayload = ticketStatusForAssign.BroadcastPayload,
//                };
//            }

//            // ── TYPE 2 / 3: Progress update ───────────────────────────
//            //var resolvedStreamName = string.IsNullOrWhiteSpace(dto.StreamName)
//            //    ? await GetDepartmentNameAsync(posterId)
//            //    : dto.StreamName;
//            var resolvedStreamName = await ResolveStreamNameAsync(dto, posterId);
//            // Auto-resolve StreamStatus from StreamName + CompletionPct
//            // UI sends StreamName ("Web Development", "QA Testing" etc.)
//            // Service computes which StatusId that maps to
//            var resolvedStatus = dto.StreamStatus.HasValue
//                 ? dto.StreamStatus.Value                                           // UI sent explicit status → use it
//                 : ResolveStreamStatus(dto.StreamName, dto.CompletionPct ?? 0);

//            // 1. Thread — optional (null/no comment = no thread)
//            var (threadId, threadCreated) =
//                await HandleThreadAsync(dto, posterId, attachmentResult);

//            // 2. Test failure / clear (no SaveChangesAsync inside — EF tracks)
//            if (dto.ReportTestFailure)
//                await HandleTestFailureAsync(dto, posterId);

//            if (dto.ClearTestFailure)
//                await HandleClearFailureAsync(dto, posterId);

//            // 3. Validate before upsert (block DevCompleted if test failed)
//            await ValidateStatusTransitionAsync(resolvedStatus, posterId, dto.IssueId);

//            // 4. Upsert poster's own WorkStream row
//            // ── Determine whether to create poster's own row or just assign ──────────
//            bool isPureAssignment = dto.NextAssigneeId.HasValue &&
//                                    dto.CompletionPct == null &&
//                                    dto.StreamStatus == null;

//            WorkStream stream;

//            if (isPureAssignment)
//            {
//                // Owner assigning someone — no row for the owner, just create assignee row
//                stream = await AssignWorkStreamAsync(
//                    issueId: dto.IssueId,
//                    assigneeId: dto.NextAssigneeId.Value,
//                    streamStatusId: dto.NextAssigneeStreamId,
//                    targetDate: dto.TargetDate
//                );
//            }
//            else
//            {
//                // Normal progress post — update poster's own row
//                stream = await UpsertStreamAsync(
//                    dto, posterId, resolvedStatus, threadId, resolvedStreamName);

//                // Also assign next person if provided
//                if (dto.NextAssigneeId.HasValue)
//                {
//                    await AssignWorkStreamAsync(
//                        issueId: dto.IssueId,
//                        assigneeId: dto.NextAssigneeId.Value,
//                        streamStatusId: dto.NextAssigneeStreamId,
//                        targetDate: dto.TargetDate
//                    );
//                }
//            }



//            // 6. Compute live ticket status (updates DB + builds broadcast payload)
//            var ticketStatus = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);

//            return BuildResponse(
//                dto, stream, resolvedStatus, threadId, threadCreated, ticketStatus);
//        });
//    }
//    catch
//    {
//        if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
//            _attachmentService.RollbackPhysicalFiles(
//                attachmentResult.PermanentFilePathsCreated);
//        throw;
//    }
//}
#endregion

#region OLD UpsertStreamAsync
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
#endregion

#region AssignWorkStreamAsync 
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
#endregion



