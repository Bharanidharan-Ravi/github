using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
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
                            decimal newPercentage = dto.TicketOverallPercentage ?? 0;
                            string actionType = "";
                            var indiaTimeZone =
                                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                            var indiaTime =
                                TimeZoneInfo.ConvertTimeFromUtc(
                                    DateTime.UtcNow,
                                    indiaTimeZone
                                );

                            var flagMasters = await _db.Set<FlagMaster>().ToListAsync();
                            var flagIds = new List<int>();
                            if (dto.IsCloseRequested)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Close Request");
                                if (flag != null) flagIds.Add(flag.Id);
                            }

                            if (dto.PriorityRequest)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Priority");
                                if (flag != null) flagIds.Add(flag.Id);
                            }

                            if (dto.FuncResponse)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Notify Functional");
                                if (flag != null) flagIds.Add(flag.Id);
                            }
                            if (dto.AdminResponse)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Notify Admin");
                                if (flag != null) flagIds.Add(flag.Id);
                            }
                            if (dto.WebResponse)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Notify Web");
                                if (flag != null) flagIds.Add(flag.Id);
                            }
                            if (dto.TechnicalResponse)
                            {
                                var flag = flagMasters.FirstOrDefault(f => f.FlagName == "Notify Technical");
                                if (flag != null) flagIds.Add(flag.Id);
                            }

                            string flagValue = flagIds.Any() ? string.Join(",", flagIds) : null;


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
                                    CreatedAt = indiaTime,
                                    Flag = flagValue
                                };

                                await _db.Set<TicketProgressLog>().AddAsync(newLog);
                                actionType = "INSERT";
                            }
                            else
                            {
                                // UPDATE EXISTING ROW: Only Percentage was provided
                                currentLog.Percentage = newPercentage;
                                currentLog.Flag = flagValue;
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
                                    RepoId = ticketStatus.RepoId,
                                    OldTicketStatus = ticketStatus.OldStatusId,
                                    NewTicketStatus = ticketStatus.ComputedStatusId,
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
                            // Optional addition here as well for pure assignment:
                            OldTicketStatus = ticketStatus.OldStatusId,
                            NewTicketStatus = ticketStatus.ComputedStatusId,
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
                    bool isTerminalAction = targetStatusId == StatusId.Closed || targetStatusId == StatusId.Cancelled;

                    if (!isTerminalAction && AppRoles.AdminManager.Contains(_loginContext.role))
                    {
                        //checkbox - D
                        if (!dto.IsSupport)
                        {
                            stream = await UpsertStreamAsync(
                                dto, posterId, targetStatusId, threadId, resolvedStreamName);
                        }
                        else
                        {
                            stream = new WorkStream
                            {
                                StreamId = Guid.Empty,
                                ResourceId = posterId,
                                StreamStatus = targetStatusId,
                                CompletionPct = 0
                            };
                        }

                        // ── WorkStream upsert ─────────────────────────────────
                        //stream = await UpsertStreamAsync(
                        //    dto, posterId, targetStatusId, threadId, resolvedStreamName);

                        //if (!dto.IsSupport)
                        //{
                        //    stream = await UpsertStreamAsync(
                        //        dto, posterId, targetStatusId, threadId, resolvedStreamName);
                        //}
                        //else if (stream == null)
                        //{
                        //    stream = new WorkStream()
                        //    {
                        //        StreamId = Guid.Empty,
                        //        ResourceId = posterId,
                        //        StreamStatus = targetStatusId,
                        //        CompletionPct = 0
                        //    };
                        //}

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

                        if (threadId > 0 && stream?.StreamId != Guid.Empty && AppRoles.AdminManager.Contains(_loginContext.role))
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
                            StreamName = targetStatusId == StatusId.Closed ? "Ticket Closed" : "Ticket Cancelled",
                            StreamStatus = targetStatusId,
                            // 👇 FIX: Use StatusId.Closed here
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
                          dto.IsReopenRequest ? posterId : null,
                          dto.IsCloseRequested,
                          dto.PriorityRequest,
                          dto.FuncResponse,
                          dto.WebResponse,
                          dto.TechnicalResponse,
                          dto.AdminResponse
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
                    toClient = dto.toClient ?? false,
                    CommentText = HtmlUtilities.ConvertToPlainText(finalHtml),
                    CompletionPct = dto.CompletionPct,
                    From_Time = dto.From_Time,
                    To_Time = dto.To_Time,
                    Hours = dto.Hours,
                    Ref_Id = dto.Ref_Id,
                    ThreadType = dto.ThreadType ?? "Comment",
                    MeetingId = dto.MeetingId,
                   
    };

                // ── ThreadMaster INSERT ───────────────────────────────────────
                var timer = _stepContext.StartStep();
                try
                {
                    await _domainService.SaveEntityWithAttachmentsAsync(
                        thread, attachmentResult?.Attachments);

                    if (dto.CoContributors != null && dto.CoContributors.Any())
                    {
                        var indiaTimeZone =
                            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

                        var indiaTime =
                            TimeZoneInfo.ConvertTimeFromUtc(
                                DateTime.UtcNow,
                                indiaTimeZone
                            );

                        // 🔥 FIX: Use 'c.id' to extract the Guid out of the incoming object
                        var coContributorRecords = dto.CoContributors.Select(c => new ThreadCoContributor
                        {
                            ThreadId = threadId,
                            EmployeeId = c.id,
                            CreatedAt = indiaTime
                        }).ToList();

                        // Insert them into the database
                        await _db.Set<ThreadCoContributor>().AddRangeAsync(coContributorRecords);
                        await _db.SaveChangesAsync();
                    }

                    if (dto.IsSupport)
                    {
                        var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                        var indiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);
                        bool alreadyAdded = dto.CoContributors?.Any(c => c.id == posterId) ?? false;
                        if (!alreadyAdded)
                        {
                            var selfContributor = new ThreadCoContributor
                            {
                                ThreadId = threadId,
                                EmployeeId = posterId,
                                CreatedAt = indiaTime
                            };

                            await _db.Set<ThreadCoContributor>().AddAsync(selfContributor);
                            await _db.SaveChangesAsync();
                        }
                    }

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

        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(
    Guid? issueId, int? forceTerminalStatusId = null, bool isReopenRequest = false, Guid? reopenedBy = null,
    bool isCloseRequested = false, bool PriorityRequest = false, bool FuncResponse = false, bool WebResponse = false,
    bool TechnicalResponse = false, bool AdminResponse = false)
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

            var ticket = await _db.Set<TicketMaster>()
                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

            int? oldTicketStatus = ticket?.Status;

            bool wasInQueue = oldTicketStatus == 18;  

            if (!subtasks.Any() && forceTerminalStatusId == null)
            {
                if (ticket != null && ticket.Status == 18)
                {
                    return new TicketStatusResult
                    {
                        OldStatusId = ticket.Status,
                        ComputedStatusId = 18,
                        ComputedStatusName = "In Queue",
                        OverallPct = (decimal)(ticket.CompletionPct ?? 0),
                        TotalSubtasks = 0,
                        CompletedSubtasks = 0,
                        ActiveSubtasks = 0,
                        TicketAutoCompleted = false,
                        RepoId = ticket.RepoId,
                    };
                }

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

            bool isExplicitlyClosed =
                forceTerminalStatusId == StatusId.Closed ||
                subtasks.Any(s => s.StreamStatus == StatusId.Closed);

            bool isExplicitlyCancelled =
                forceTerminalStatusId == StatusId.Cancelled ||
                subtasks.Any(s => s.StreamStatus == StatusId.Cancelled);

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

            if (wasInQueue)
            {
                computedStatusId = 18;
                computedStatusName = "In Queue";
            }

            // reopen override
            if (isReopenRequest && ticket != null)
            {
                int? ownerTeamId = await GetDepartmentNameAsync(ticket.Assignee_Id);

                if (ownerTeamId == 1)
                {
                    computedStatusId = 11;
                    computedStatusName = "Functional Support";
                }
                else
                {
                    computedStatusId = 5;
                    computedStatusName = "In Development";
                }

                overallPct = 0;
            }

            bool isTerminal = computedStatusId == StatusId.Closed || computedStatusId == StatusId.Cancelled;

            if (ticket != null)
            {
                var timer = _stepContext.StartStep();

                try
                {
                    await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                        t => t.Issue_Id == issueId,
                        t =>
                        {
                            t.Status = computedStatusId;
                            t.StatusName = computedStatusName;
                            t.CompletionPct = (decimal?)overallPct;

                            if (isReopenRequest)
                            {
                                t.ReopenCount += 1;
                                t.ReopenedBy = reopenedBy;
                            }

                            t.IsCloseRequested = isCloseRequested;
                            t.PriorityRequest = PriorityRequest;
                            t.FuncResponse = FuncResponse;
                            t.WebResponse = WebResponse;
                            t.TechnicalResponse = TechnicalResponse;
                            t.AdminResponse = AdminResponse;

                            if (isExplicitlyClosed || isExplicitlyCancelled)
                            {
                                t.IsCloseRequested = false;
                                t.PriorityRequest = false;
                                t.FuncResponse = false;
                                t.WebResponse = false;
                                t.TechnicalResponse = false;
                                t.AdminResponse = false;
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

            string repoKey = string.Empty;

            if (ticket?.RepoId != null)
            {
                repoKey = await _db.RepositoryMasters
                    .Where(r => r.Repo_Id == ticket.RepoId)
                    .Select(r => r.RepoKey)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            return new TicketStatusResult
            {
                OldStatusId = oldTicketStatus,
                ComputedStatusId = computedStatusId,
                ComputedStatusName = computedStatusName,
                OverallPct = (decimal)overallPct,
                TotalSubtasks = totalSubtasks,
                CompletedSubtasks = completedSubtasks,
                ActiveSubtasks = activeSubtasks,
                TicketAutoCompleted = false,
                RepoKey = repoKey,
                IsTerminal = isTerminal,
                RepoId = ticket?.RepoId,
                BroadcastPayload = isTerminal ? null : new
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
                }
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

                // 👇 ADDED: Map the old and new statuses back to the response
                OldTicketStatus = ticketStatus.OldStatusId,
                NewTicketStatus = ticketStatus.ComputedStatusId,

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
                RepoId = ticketStatus.RepoId,
                IsTerminal = ticketStatus.IsTerminal,
                BroadcastPayload = ticketStatus.BroadcastPayload,
            };
        }
        public async Task<long> PostMeetingScheduledAsync(
            MeetingMaster meeting,
            List<MeetingAttendance> attendance,
            Guid createdBy)
        {
            try
            {
                if (meeting == null)
                    throw new ArgumentNullException(nameof(meeting));

                if (!meeting.ticket_id.HasValue)
                    throw new InvalidOperationException("Meeting is not linked to a ticket.");

                var postDto = BuildMeetingPostDto(
                    meeting,
                    createdBy,
                    BuildMeetingScheduledComment(meeting),
                    null,
                    null,
                    null,
                    BuildCoContributors(attendance, createdBy));

                var response = await PostWorkStreamAsync(postDto);

                if (response == null)
                    throw new Exception("WorkStream returned a null response.");

                if (!response.ThreadId.HasValue)
                    throw new Exception("Meeting thread was not created successfully.");

                return response.ThreadId.Value;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Failed to create the meeting thread for MeetingId '{meeting?.meeting_id}'. {ex.Message}",
                    ex);
            }
        }

        public async Task UpdateMeetingThreadAsync(
            MeetingMaster meeting,
            List<MeetingAttendance> attendance,
            Guid updatedBy)
        {
            if (!meeting.ticket_id.HasValue || !meeting.ThreadId.HasValue)
                return;

            var postDto = BuildMeetingPostDto(
                meeting,
                updatedBy,
                BuildMeetingScheduledComment(meeting),
                null,
                null,
                null,
                BuildCoContributors(attendance, updatedBy));

            await UpdateMeetingThreadCoreAsync(meeting.ThreadId.Value, postDto);
        }

        public async Task UpdateMeetingCompletionThreadAsync(
            MeetingMaster meeting,
            MeetingCompletionDto dto,
            Guid completedBy)
        {
            if (!meeting.ticket_id.HasValue || !meeting.ThreadId.HasValue)
                return;

            var attendance = await _domainService
                .Query<MeetingAttendance>()
                .Where(x =>
                    x.meeting_id == meeting.meeting_id &&
                    x.attendance_status == "Present" &&
                    x.invite_status == "Accepted")
                .ToListAsync();

            var duration = dto.ActualEndTime - dto.ActualStartTime;

            var postDto = BuildMeetingPostDto(
                meeting,
                completedBy,
                BuildMeetingCompletionComment(meeting, dto, duration),
                dto.ActualStartTime,
                dto.ActualEndTime,
                $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}",
                BuildCoContributors(attendance, null));

            await UpdateMeetingThreadCoreAsync(meeting.ThreadId.Value, postDto);
        }

        private PostWorkStreamDto BuildMeetingPostDto(
            MeetingMaster meeting,
            Guid resourceId,
            string comment,
            DateTime? fromTime,
            DateTime? toTime,
            string? hours,
            List<CoContributorItemDto> coContributors)
        {
            return new PostWorkStreamDto
            {
                IssueId = meeting.ticket_id.Value,
                ResourceId = resourceId,
                StreamName = "Meeting",
                CommentText = comment,
                From_Time = fromTime,
                To_Time = toTime,
                Hours = hours,
                UseLastThread = false,
                ThreadType = "Meeting",
                MeetingId = meeting.meeting_id,
                CoContributors = coContributors
            };
        }

        private async Task UpdateMeetingThreadCoreAsync(long threadId, PostWorkStreamDto postDto)
        {
            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.UpdateTrackedEntityAsync<ThreadMaster>(
                    x => x.ThreadId == threadId,
                    x =>
                    {
                        x.HtmlDesc = postDto.CommentText;
                        x.CommentText = HtmlUtilities.ConvertToPlainText(postDto.CommentText);
                        x.ThreadType = postDto.ThreadType;
                        x.MeetingId = postDto.MeetingId;
                        x.From_Time = postDto.From_Time;
                        x.To_Time = postDto.To_Time;
                        x.Hours = postDto.Hours;
                    });

                var existing = await _db.Set<ThreadCoContributor>()
                    .Where(x => x.ThreadId == threadId)
                    .ToListAsync();

                if (existing.Any())
                    _db.Set<ThreadCoContributor>().RemoveRange(existing);

                if (postDto.CoContributors != null && postDto.CoContributors.Any())
                {
                    var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    var indiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);

                    var contributors = postDto.CoContributors
                        .Select(x => x.id)
                        .Distinct()
                        .Select(id => new ThreadCoContributor
                        {
                            ThreadId = threadId,
                            EmployeeId = id,
                            CreatedAt = indiaTime
                        })
                        .ToList();

                    await _db.Set<ThreadCoContributor>().AddRangeAsync(contributors);
                }

                await _db.SaveChangesAsync();

                _stepContext.Success("ThreadMaster", "UPDATE", threadId.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("ThreadMaster", "UPDATE",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }
        }

        private List<CoContributorItemDto> BuildCoContributors(
      List<MeetingAttendance> attendance,
      Guid? createdBy)
        {
            return attendance
                .Where(x =>
                    x.participant_type == "Employee" &&
                    x.participant_id != createdBy)
                .Select(x => new CoContributorItemDto
                {
                    id = x.participant_id
                })
                .ToList();
        }

        private static string BuildMeetingScheduledComment(MeetingMaster meeting)
        {
            return $@"Meeting Scheduled

Meeting : {meeting.title}
Date : {meeting.meeting_date:yyyy-MM-dd}
Time : {meeting.start_time} - {meeting.end_time}
Duration : {meeting.slot_duration}
Summary : {meeting.meeting_summary}";
        }

        private static string BuildMeetingCompletionComment(
            MeetingMaster meeting,
            MeetingCompletionDto dto,
            TimeSpan duration)
        {
            return $@"Meeting Completed

Meeting : {meeting.title}
Start : {dto.ActualStartTime:yyyy-MM-dd HH:mm}
End : {dto.ActualEndTime:yyyy-MM-dd HH:mm}
Duration : {(int)duration.TotalHours:D2}:{duration.Minutes:D2}

Summary
{dto.MeetingSummary}";
        }
    }
}
