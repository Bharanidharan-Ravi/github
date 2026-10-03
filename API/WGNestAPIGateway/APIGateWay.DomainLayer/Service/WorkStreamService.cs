
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;
using ReverseMarkdown.Converters;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

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
            IRequestStepContext stepContext                        // ← ADDED
            )
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
            var issueLogPermanentPaths = new List<string>();
            string oldFlagIds = null;
            string newFlagIds = null;
            string actionType = "";
            var indiaTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var indiaTime =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    indiaTimeZone
                );
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
                            string oldFlagValue = currentLog?.Flag;


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
                            oldFlagIds = oldFlagValue;
                            newFlagIds = flagValue;

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
                    // Issue Logger submits its new issues with the thread form: the
                    // comment becomes "Issue:" (each new issue as #IssueLogId +
                    // description) followed by "General comments:" (what was typed
                    // in the form), so one thread holds them all — and one is
                    // created even when no comment was typed.
                    var (threadId, threadCreated) =
                        await HandleThreadAsync(dto, posterId, handoffToUpdate?.HandsOffId, attachmentResult, issueLogPermanentPaths);



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

                        //if (threadId > 0 && stream?.StreamId != Guid.Empty && AppRoles.AdminManager.Contains(_loginContext.role))
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
                          //dto.Move_to,
                          isTerminalAction ? targetStatusId : null,
                          dto.IsReopenRequest, // Pass the flag from UI
                          dto.IsReopenRequest ? posterId : null,
                          dto.IsCloseRequested,
                          dto.PriorityRequest,
                          dto.FuncResponse,
                          dto.WebResponse,
                          dto.TechnicalResponse,
                          dto.AdminResponse,
                          dto.toClient
                      );

                    //return BuildResponse(dto, stream, targetStatusId, threadId, threadCreated, ticketStatus2);
                    var response = BuildResponse(dto, stream, targetStatusId, threadId, threadCreated, ticketStatus2);
                    if (AppRoles.AdminManager.Contains(_loginContext.role))
                    {
                        response.OldFlagIds = oldFlagIds;
                        response.NewFlagIds = newFlagIds;
                    }


                    return response;
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                if (issueLogPermanentPaths.Any())
                    _attachmentService.RollbackPhysicalFiles(issueLogPermanentPaths);
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
            ProcessedAttachmentResult? attachmentResult,
            List<string> issueLogPermanentPaths)
        {
            var issueLogs = dto.IssueLogs?.Where(i => !string.IsNullOrWhiteSpace(i.Description)).ToList()
                ?? new List<BulkIssueLogItemDto>();
            var issueUpdates = dto.IssueLogUpdates?.Where(u => u.IssueLogId > 0).ToList()
                ?? new List<BulkIssueLogUpdateItemDto>();

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

            if (!string.IsNullOrWhiteSpace(dto.CommentText) || issueLogs.Any() || issueUpdates.Any())
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
                    if (attachmentResult.PermanentFilePathsCreated?.Any() == true)
                        issueLogPermanentPaths.AddRange(attachmentResult.PermanentFilePathsCreated);
                }

                // ── Issue Logger: one IssueLog row per new issue and one
                // ISSUEACTIVITY row per edited issue, all on this thread; the
                // thread's comment then shows a "New issues" and an "Updated
                // issues" table, then "General comments".
                if (issueLogs.Any() || issueUpdates.Any())
                {
                    var created = new List<IssueThreadRow>();
                    foreach (var item in issueLogs)
                        created.Add(await InsertIssueLogAsync(dto.IssueId, dto.Hours, threadId, posterId, item, issueLogPermanentPaths));

                    var updated = new List<IssueThreadRow>();
                    foreach (var item in issueUpdates)
                        updated.Add(await InsertIssueUpdateActivityAsync(dto.IssueId, dto.Hours, threadId, posterId, item, issueLogPermanentPaths));

                    finalHtml = BuildIssueThreadHtml(created, updated, finalHtml);
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
                    ThreadFor=dto.ThreadFor,
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

                // Disabled for now — reply → issue remarks. When on, a reply to a
                // thread with issues on it becomes a remark on each of those issues
                // (an Issue Logger submit never replies). Uncomment to turn it back on,
                // together with the reply banner hint in TicketThreads.jsx.
                //if (!issueLogs.Any() && !issueUpdates.Any())
                //    await AddReplyRemarksAsync(dto, threadId, finalHtml, posterId);

                if (dto.temp?.temps != null && dto.temp.temps.Any())
                    await _attachmentService.CleanupTempFiles(dto.temp);

                return (threadId, true);
            }

            return (0, false);
        }

        // =====================================================================
        // INSERT ISSUE LOG — one row for the "Issue Logger" tab, linked to the
        // thread created in the same request. IssueLogId is an IDENTITY column,
        // so the row is saved first to get the number its attachment folder and
        // the thread comment use; then its temp files are copied to permanent
        // storage and their URLs rewritten in Html. Its status, priority,
        // remarks and hours go in its first ISSUEACTIVITY row. Returns the
        // issue as the thread comment shows it.
        // =====================================================================
        private async Task<IssueThreadRow> InsertIssueLogAsync(
            Guid issueId, string hours, long threadId, Guid posterId, BulkIssueLogItemDto item, List<string> permanentPaths)
        {
            // Next number within this ticket. Each issue is saved before the next
            // one, so several issues in one submit get 1, 2, 3 in order.
            var lastNo = await _db.IssueLog
                .Where(x => x.Issue_Id == issueId)
                .MaxAsync(x => (int?)x.IssueLogId) ?? 0;

            var issueLog = new IssueLog
            {
                Issue_Id = issueId,
                IssueLogId = lastNo + 1,
                Status = string.IsNullOrWhiteSpace(item.Status) ? "Open" : item.Status,
                Html = item.Description,
                Description = GetIssueText(item.Description),
            };

            var timer = _stepContext.StartStep();
            try
            {
                await _domainService.SaveEntityWithAttachmentsAsync(issueLog, null);

                if (item.temp?.temps != null && item.temp.temps.Any())
                {
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var relativePath = $"{permUserId}/{issueLog.IssueLogId}-{issueId}";

                    var result = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        item.Description, item.temp.temps, relativePath,
                        issueLog.IssueLogId.ToString(), "IssueLog");

                    if (result.PermanentFilePathsCreated?.Any() == true)
                        permanentPaths.AddRange(result.PermanentFilePathsCreated);

                    issueLog.Html = result.UpdatedHtml;
                    _db.IssueLog.Update(issueLog);
                    if (result.Attachments?.Any() == true)
                        _db.AttachmentMaster.AddRange(result.Attachments);
                    await _db.SaveChangesAsync();

                    await _attachmentService.CleanupTempFiles(item.temp);
                }

                _stepContext.Success("IssueLog", "INSERT", issueLog.IssueLogId.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("IssueLog", "INSERT",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            // ── ISSUEACTIVITY INSERT ──────────────────────────────────────────
            var activity = new IssueActivity
            {
                Issue_Id = issueId,
                ThreadId = threadId,
                IssuelogId = issueLog.IssueLogId,
                Hours = hours,
                Status = string.IsNullOrWhiteSpace(item.Status) ? "Open" : item.Status,
                Priority = string.IsNullOrWhiteSpace(item.Priority) ? "Medium" : item.Priority,
                Remarks = item.Remarks,
                ChangedBy = posterId,
            };

            var activityTimer = _stepContext.StartStep();
            try
            {
                _db.IssueActivity.Add(activity);
                await _db.SaveChangesAsync();
                _stepContext.Success("IssueActivity", "INSERT", activity.ActivityId.ToString(), activityTimer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("IssueActivity", "INSERT",
                    ex.Message, ex.InnerException?.Message, activityTimer);
                throw;
            }

            return new IssueThreadRow(
                issueLog.IssueLogId, issueLog.Description,
                activity.Status, activity.Priority, activity.Remarks, issueLog.Html);
        }

        // The thread form's Total Hours arrives as "HH:MM"; ISSUEACTIVITY.Hours is
        // decimal hours, so "01:30" → 1.50. Blank or unreadable → null (none logged).
        //private static decimal? ToDecimalHours(string? value)
        //{
        //    if (string.IsNullOrWhiteSpace(value)) return null;

        //    var parts = value.Trim().Split(':');
        //    if (parts.Length >= 2 &&
        //        int.TryParse(parts[0], out var h) &&
        //        int.TryParse(parts[1], out var m))
        //        return Math.Round(h + m / 60m, 2);

        //    return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
        //        ? d : null;
        //}

        // One issue as the thread comment shows it. Only the <img> / file-attachment
        // links in AttachmentsHtml are used. For an edit (IsUpdate), OldStatus /
        // OldPriority are the values before it, so a changed field shows old → new.
        private sealed record IssueThreadRow(
            int IssueLogId, string? Description,
            string? Status, string? Priority, string? Remarks, string? AttachmentsHtml,
            bool IsUpdate = false, string? OldStatus = null, string? OldPriority = null);

        // =====================================================================
        // INSERT ISSUE UPDATE ACTIVITY — an issue edited inline in the Issue
        // Logger. The IssueLog row is left as it is; the edit becomes a new
        // ISSUEACTIVITY row on the thread created in the same request. Status /
        // Priority left blank keep the issue's current value (its latest
        // activity that set one), so every row carries the full state. Files
        // added with the edit are copied to their own folder (the row is saved
        // first for its ActivityId) and kept on the row as AttachmentsHtml.
        // =====================================================================
        private async Task<IssueThreadRow> InsertIssueUpdateActivityAsync(
            Guid issueId, string Hours, long threadId, Guid posterId, BulkIssueLogUpdateItemDto item, List<string> permanentPaths)
        {
            var issueLog = await _db.IssueLog
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Issue_Id == issueId && x.IssueLogId == item.IssueLogId)
                ?? throw new InvalidOperationException($"Issue #{item.IssueLogId} was not found on this ticket.");

            var (oldStatus, oldPriority) =
                (await GetCurrentIssueStatesAsync(issueId, new[] { issueLog.IssueLogId }))[issueLog.IssueLogId];

            var activity = new IssueActivity
            {
                Issue_Id = issueId,
                ThreadId = threadId,
                Hours = Hours,
                IssuelogId = issueLog.IssueLogId,
                Status = string.IsNullOrWhiteSpace(item.Status) ? oldStatus : item.Status,
                Priority = string.IsNullOrWhiteSpace(item.Priority) ? oldPriority : item.Priority,
                Remarks = string.IsNullOrWhiteSpace(item.Remarks) ? null : item.Remarks.Trim(),
                ChangedBy = posterId,
            };

            var timer = _stepContext.StartStep();
            try
            {
                _db.IssueActivity.Add(activity);
                await _db.SaveChangesAsync();

                if (item.temp?.temps != null && item.temp.temps.Any())
                {
                    // Own folder per edit: files are stored by name, so one called
                    // like an earlier file of the issue must not overwrite it.
                    var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                    var relativePath = $"{permUserId}/{issueLog.IssueLogId}-{issueId}/activity-{activity.ActivityId}";

                    var result = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                        item.AttachmentsHtml ?? "", item.temp.temps, relativePath,
                        activity.ActivityId.ToString(), "IssueActivity");

                    if (result.PermanentFilePathsCreated?.Any() == true)
                        permanentPaths.AddRange(result.PermanentFilePathsCreated);

                    activity.AttachmentsHtml = string.IsNullOrWhiteSpace(result.UpdatedHtml) ? null : result.UpdatedHtml;
                    if (result.Attachments?.Any() == true)
                        _db.AttachmentMaster.AddRange(result.Attachments);
                    await _db.SaveChangesAsync();

                    await _attachmentService.CleanupTempFiles(item.temp);
                }

                _stepContext.Success("IssueActivity", "INSERT", activity.ActivityId.ToString(), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("IssueActivity", "INSERT",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }

            // Older IssueLog rows may hold HTML in Description; the table shows text.
            return new IssueThreadRow(
                issueLog.IssueLogId, GetIssueText(issueLog.Description),
                activity.Status, activity.Priority, activity.Remarks, activity.AttachmentsHtml,
                IsUpdate: true, OldStatus: oldStatus, OldPriority: oldPriority);
        }

        // Each issue's current Status / Priority: from its latest ISSUEACTIVITY row
        // that set one, else (Status only) the IssueLog row's own value.
        private async Task<Dictionary<int, (string? Status, string? Priority)>> GetCurrentIssueStatesAsync(
            Guid issueId, IReadOnlyCollection<int> issueLogIds)
        {
            var ids = issueLogIds.ToList();

            var activities = await _db.IssueActivity
                .Where(a => a.Issue_Id == issueId && ids.Contains(a.IssuelogId))
                .OrderByDescending(a => a.ActivityId)
                .Select(a => new { a.IssuelogId, a.Status, a.Priority })
                .ToListAsync();

            var issueStatuses = await _db.IssueLog
                .Where(x => x.Issue_Id == issueId && ids.Contains(x.IssueLogId))
                .ToDictionaryAsync(x => x.IssueLogId, x => x.Status);

            return ids.ToDictionary(id => id, id =>
            {
                var rows = activities.Where(a => a.IssuelogId == id).ToList();
                return (
                    rows.Select(a => a.Status).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                        ?? issueStatuses.GetValueOrDefault(id),
                    rows.Select(a => a.Priority).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)));
            });
        }

        // =====================================================================
        // REPLY REMARKS — a reply (Ref_Id) to a thread that has Issue Logger
        // issues on it (ISSUEACTIVITY rows with that ThreadId) adds a new
        // ISSUEACTIVITY row to each of those issues, linked to the reply: its
        // text becomes the Remarks and its files the AttachmentsHtml, while
        // Status / Priority keep each issue's current values. A reply with
        // neither text nor files (e.g. hours only) adds nothing. Replying to
        // such a reply works the same way, since it has activity rows too.
        // =====================================================================
        private async Task AddReplyRemarksAsync(
            PostWorkStreamDto dto, long replyThreadId, string? finalHtml, Guid posterId)
        {
            if (!long.TryParse(dto.Ref_Id, out var refThreadId)) return;

            var issueLogIds = await _db.IssueActivity
                .Where(a => a.Issue_Id == dto.IssueId && a.ThreadId == refThreadId)
                .Select(a => a.IssuelogId)
                .Distinct()
                .ToListAsync();
            if (!issueLogIds.Any()) return;

            // finalHtml already carries the permanent URLs of the reply's files.
            var remark = GetIssueText(finalHtml);
            var files = string.Concat(AttachmentTagRegex.Matches(finalHtml ?? "").Select(m => m.Value));
            if (string.IsNullOrWhiteSpace(remark) && files.Length == 0) return;

            var states = await GetCurrentIssueStatesAsync(dto.IssueId, issueLogIds);
            var activities = issueLogIds.OrderBy(id => id).Select(id => new IssueActivity
            {
                Issue_Id = dto.IssueId,
                ThreadId = replyThreadId,
                IssuelogId = id,
                Status = states[id].Status,
                Priority = states[id].Priority,
                Remarks = string.IsNullOrWhiteSpace(remark) ? null : remark,
                AttachmentsHtml = files.Length == 0 ? null : files,
                Hours = dto.Hours,
                ChangedBy = posterId,
            }).ToList();

            var timer = _stepContext.StartStep();
            try
            {
                _db.IssueActivity.AddRange(activities);
                await _db.SaveChangesAsync();
                _stepContext.Success("IssueActivity", "INSERT",
                    string.Join(",", activities.Select(a => a.ActivityId)), timer);
            }
            catch (Exception ex)
            {
                _stepContext.Failure("IssueActivity", "INSERT",
                    ex.Message, ex.InnerException?.Message, timer);
                throw;
            }
        }


        // Image / file-attachment tags as the Issue Logger writes them into HTML.
        private static readonly Regex AttachmentTagRegex = new(
            @"<img\b[^>]*>|<a\b[^>]*data-type=""file-attachment""[^>]*>.*?</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // An issue's plain-text description: its HTML without the attachments.
        private static string GetIssueText(string? html) =>
            HtmlUtilities.ConvertToPlainText(AttachmentTagRegex.Replace(html ?? "", "")) ?? "";

        private static string ToHtmlText(string? text) =>
            WebUtility.HtmlEncode(text ?? "").Replace("\r\n", "\n").Replace("\n", "<br>");

        // Badge colours (background, text), the same as the Issue Logger's badges.
        private static readonly Dictionary<string, (string Bg, string Fg)> StatusBadgeColors =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Open"] = ("#fef2f2", "#b91c1c"),
                ["In Progress"] = ("#fffbeb", "#b45309"),
                ["Retest"] = ("#eff6ff", "#1d4ed8"),
                ["Passed"] = ("#ecfdf5", "#047857"),
                ["Failed"] = ("#fff1f2", "#be123c"),
                ["Not Required"] = ("#f1f5f9", "#475569"),
                ["Closed"] = ("#f0fdf4", "#15803d"),
            };

        private static readonly Dictionary<string, (string Bg, string Fg)> PriorityBadgeColors =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Low"] = ("#f8fafc", "#475569"),
                ["Medium"] = ("#f0f9ff", "#0369a1"),
                ["High"] = ("#fff7ed", "#c2410c"),
                ["Critical"] = ("#dc2626", "#ffffff"),
            };

        private const string EmptyCellHtml = "<span style=\"color:#94a3b8\">—</span>";

        private static string BadgeHtml(string? value, Dictionary<string, (string Bg, string Fg)> colors)
        {
            if (string.IsNullOrWhiteSpace(value)) return EmptyCellHtml;
            var (bg, fg) = colors.TryGetValue(value, out var c) ? c : ("#f1f5f9", "#475569");
            return $"<span style=\"display:inline-block;padding:1px 8px;border-radius:9999px;" +
                   $"font-size:11px;font-weight:600;white-space:nowrap;background:{bg};color:{fg}\">" +
                   $"{WebUtility.HtmlEncode(value)}</span>";
        }

        // A field the edit changed shows as the old value struck through → the new
        // badge; anything else (or a new issue) shows just its badge.
        private static string FieldHtml(
            bool isUpdate, string? oldValue, string? value, Dictionary<string, (string Bg, string Fg)> colors)
        {
            if (!isUpdate || string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
                return BadgeHtml(value, colors);

            var old = string.IsNullOrWhiteSpace(oldValue)
                ? EmptyCellHtml
                : $"<s style=\"color:#94a3b8\">{WebUtility.HtmlEncode(oldValue)}</s>";
            return $"{old} → {BadgeHtml(value, colors)}";
        }

        // One attachment per line in a table cell. Images become small thumbnails
        // (the thread view would otherwise show them up to 200px tall); file
        // links keep their pill.
        private static string AttachmentCellHtml(string attachmentTag) =>
            "<div style=\"margin:2px 0\">" +
            (attachmentTag.StartsWith("<img", StringComparison.OrdinalIgnoreCase)
                ? "<img style=\"max-height:56px\"" + attachmentTag[4..]
                : attachmentTag) +
            "</div>";

        // One section: a heading with the count, then a table with a row per issue.
        // The Remarks / Attachments columns only appear when some issue has one.
        // The table is exactly as wide as the thread (fixed layout), so long text
        // wraps onto the next line in its column instead of widening the table:
        // #, Status, Priority and Attachments have fixed widths (a badge never
        // splits) and Issue / Remarks share the rest. Only when the thread is
        // too narrow to give each of those at least 100px (phones) does the
        // table keep that minimum and scroll sideways instead.
        private const string IssueCellStyle = "padding:6px 10px";

        private static void AppendIssueTable(StringBuilder sb, string title, List<IssueThreadRow> rows)
        {
            if (!rows.Any()) return;

            var items = rows
                .Select(r => (Row: r, Files: AttachmentTagRegex.Matches(r.AttachmentsHtml ?? "")
                    .Select(m => AttachmentCellHtml(m.Value)).ToList()))
                .ToList();
            bool showRemarks = rows.Any(r => !string.IsNullOrWhiteSpace(r.Remarks));
            bool showFiles = items.Any(i => i.Files.Any());

            sb.Append($"<p><strong>{title}</strong> <span style=\"color:#64748b\">({rows.Count})</span></p>");
            string th(string text) => $"<th style=\"{IssueCellStyle}\">{text}</th>";
            string td(string html) => $"<td style=\"{IssueCellStyle}\">{html}</td>";

            int minWidth = 48 + 124 + 96 + 100 + (showRemarks ? 100 : 0) + (showFiles ? 120 : 0);

            sb.Append($"<table style=\"width:100%;min-width:{minWidth}px;table-layout:fixed;overflow-wrap:break-word;margin-top:0\">")
              .Append("<colgroup><col style=\"width:48px\"><col><col style=\"width:124px\"><col style=\"width:96px\">");
            if (showRemarks) sb.Append("<col>");
            if (showFiles) sb.Append("<col style=\"width:120px\">");
            sb.Append("</colgroup><thead><tr>")
              .Append(th("#")).Append(th("Issue")).Append(th("Status")).Append(th("Priority"));
            if (showRemarks) sb.Append(th("Remarks"));
            if (showFiles) sb.Append(th("Attachments"));
            sb.Append("</tr></thead><tbody>");

            foreach (var (r, files) in items)
            {
                sb.Append("<tr>")
                  .Append(td($"<strong>#{r.IssueLogId}</strong>"))
                  .Append(td(ToHtmlText(r.Description)))
                  .Append(td(FieldHtml(r.IsUpdate, r.OldStatus, r.Status, StatusBadgeColors)))
                  .Append(td(FieldHtml(r.IsUpdate, r.OldPriority, r.Priority, PriorityBadgeColors)));
                if (showRemarks)
                    sb.Append(td(string.IsNullOrWhiteSpace(r.Remarks) ? EmptyCellHtml : ToHtmlText(r.Remarks)));
                if (showFiles)
                    sb.Append(td(files.Any() ? string.Concat(files) : EmptyCellHtml));
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
        }

        // Thread comment for an Issue Logger submit, each section only when it has something:
        //   New issues (n)       # | Issue | Status | Priority | Remarks | Attachments
        //   Updated issues (n)   same columns; a changed Status / Priority shows as
        //                        ~~old~~ → new
        //   General comments     what was typed in the form, with its attachments
        // Attachment links already carry their permanent URLs (temp files were
        // copied when each row was saved), so the thread shows the same files.
        private static string BuildIssueThreadHtml(
            List<IssueThreadRow> created, List<IssueThreadRow> updated, string? generalCommentsHtml)
        {
            var sb = new StringBuilder();

            AppendIssueTable(sb, "New issues", created);
            AppendIssueTable(sb, "Updated issues", updated);

            bool hasGeneralComments = !string.IsNullOrWhiteSpace(generalCommentsHtml) &&
                (!string.IsNullOrWhiteSpace(GetIssueText(generalCommentsHtml)) ||
                 AttachmentTagRegex.IsMatch(generalCommentsHtml));

            if (hasGeneralComments)
                sb.Append("<p><strong>General comments</strong></p>").Append(generalCommentsHtml);

            return sb.ToString();
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
    bool TechnicalResponse = false, bool AdminResponse = false, bool? toClient = false, bool updateTicketFlags = true)
        {
            if (isReopenRequest)
            {
                var staleSubtasks = await _db.WorkStreams
                    .Where(ws => ws.IssueId == issueId &&
                    (ws.StreamStatus == StatusId.Closed || ws.StreamStatus == StatusId.Cancelled))
                    .ToListAsync();
                foreach (var s in staleSubtasks)
                    s.StreamStatus = StatusId.Inactive;
                if (staleSubtasks.Count > 0)
                    await _db.SaveChangesAsync();

            }
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
                            //if (move_to !=null)
                            //{
                            //    t.Move_to = move_to;
                            //}
                            if (!t.RaiseToClient && (toClient ?? false))
                            {
                                t.RaiseToClient = true;
                            }

                            if (isReopenRequest)
                            {
                                t.ReopenCount += 1;
                                t.ReopenedBy = reopenedBy;
                            }
                            if (updateTicketFlags)
                            {
                                t.IsCloseRequested = isCloseRequested;
                                t.PriorityRequest = PriorityRequest;
                                t.FuncResponse = FuncResponse;
                                t.WebResponse = WebResponse;
                                t.TechnicalResponse = TechnicalResponse;
                                t.AdminResponse = AdminResponse;
                            }
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
        public async Task<WorkStreamResult> UpsertWorkStreamsAsync(WorkStreamContext ctx, bool updateTicketFlags = true)
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

                var ticketStatus1 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId, updateTicketFlags: updateTicketFlags);

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

                var ticketStatus2 = await ComputeAndUpdateTicketStatusAsync(ctx.IssueId, updateTicketFlags: updateTicketFlags);

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

        // Ends a DAILY / WEEKLY series on its main thread. Each logged day already
        // has its own thread with that day's times and hours, so this one keeps none.
        public async Task CloseMeetingSeriesThreadAsync(
            MeetingMaster meeting,
            DateTime lastDay,
            Guid completedBy)
        {
            if (!meeting.ticket_id.HasValue || !meeting.ThreadId.HasValue)
                return;

            var attendance = await _domainService
                .Query<MeetingAttendance>()
                .Where(x => x.meeting_id == meeting.meeting_id)
                .ToListAsync();

            var postDto = BuildMeetingPostDto(
                meeting,
                completedBy,
                BuildMeetingSeriesEndedComment(meeting, lastDay),
                null,
                null,
                null,
                BuildCoContributors(attendance, meeting.created_by));

            await UpdateMeetingThreadCoreAsync(meeting.ThreadId.Value, postDto);
        }

        // A day comment on a DAILY / WEEKLY meeting becomes a new "Meeting" thread on
        // the ticket, holding that sitting's times, summary and attendance. A day can
        // have several (the meeting can be held more than once), so earlier comments
        // for the same day are never overwritten.
        public async Task<long> PostMeetingOccurrenceThreadAsync(
            MeetingMaster meeting,
            MeetingCompletionDto dto,
            Guid completedBy)
        {
            if (!meeting.ticket_id.HasValue)
                throw new InvalidOperationException("Meeting is not linked to a ticket.");

            // The form sends times on today's date; move them onto the meeting day.
            var day = dto.OccurrenceDate!.Value.Date;
            var start = day + dto.ActualStartTime.TimeOfDay;
            var end = day + dto.ActualEndTime.TimeOfDay;
            var duration = end - start;

            var presentIds = (dto.Attendance ?? new List<MeetingAttendanceUpdateDto>())
                .Where(x => x.AttendanceStatus == "Present")
                .Select(x => x.ParticipantId)
                .ToList();

            var invited = await _domainService
                .Query<MeetingAttendance>()
                .Where(x => x.meeting_id == meeting.meeting_id)
                .ToListAsync();

            var present = invited
                .Where(x => presentIds.Contains(x.participant_id))
                .ToList();

            var postDto = BuildMeetingPostDto(
                meeting,
                completedBy,
                BuildMeetingOccurrenceComment(meeting, dto.MeetingSummary, start, end, duration, present.Count, invited.Count),
                start,
                end,
                $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}",
                BuildCoContributors(present, null));

            var response = await PostWorkStreamAsync(postDto);

            if (response?.ThreadId == null)
                throw new Exception($"The thread for {day:dd MMM yyyy} was not created.");

            return response.ThreadId.Value;
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
            // DAILY / WEEKLY meetings have no meeting_date, only a date range.
            var date = meeting.recurrence_type?.ToUpperInvariant() switch
            {
                "DAILY" => $"Every day, {meeting.valid_from_date:yyyy-MM-dd} to {meeting.valid_to_date:yyyy-MM-dd}",
                "WEEKLY" => $"Every {WeekdayNames(meeting.days_of_week)}, {meeting.valid_from_date:yyyy-MM-dd} to {meeting.valid_to_date:yyyy-MM-dd}",
                _ => $"{meeting.meeting_date:yyyy-MM-dd}",
            };

            // The password is left out on purpose: ticket threads can have a wider audience.
            var joinLine = string.IsNullOrWhiteSpace(meeting.meet_link)
                ? ""
                : MeetingLineHtml("Join", meeting.meet_link);

            return MeetingHeadingHtml("Meeting Scheduled")
                + MeetingLineHtml("Meeting", meeting.title)
                + MeetingLineHtml("Date", date)
                + MeetingLineHtml("Time", $"{meeting.start_time} - {meeting.end_time}")
                + MeetingLineHtml("Duration", $"{meeting.slot_duration}")
                + joinLine
                + MeetingSummaryHtml(meeting.meeting_summary);
        }

        private static string BuildMeetingCompletionComment(
            MeetingMaster meeting,
            MeetingCompletionDto dto,
            TimeSpan duration)
        {
            return MeetingHeadingHtml("Meeting Completed")
                + MeetingLineHtml("Meeting", meeting.title)
                + MeetingLineHtml("Start", $"{dto.ActualStartTime:yyyy-MM-dd HH:mm}")
                + MeetingLineHtml("End", $"{dto.ActualEndTime:yyyy-MM-dd HH:mm}")
                + MeetingLineHtml("Duration", $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}")
                + MeetingSummaryHtml(dto.MeetingSummary);
        }

        // Starts with "Meeting Completed" so the ticket thread card shows it as done.
        private static string BuildMeetingOccurrenceComment(
            MeetingMaster meeting,
            string? summary,
            DateTime start,
            DateTime end,
            TimeSpan duration,
            int presentCount,
            int invitedCount)
        {
            return MeetingHeadingHtml("Meeting Completed")
                + MeetingLineHtml("Meeting", meeting.title)
                + MeetingLineHtml("Day", $"{start:ddd, dd MMM yyyy}")
                + MeetingLineHtml("Start", $"{start:yyyy-MM-dd HH:mm}")
                + MeetingLineHtml("End", $"{end:yyyy-MM-dd HH:mm}")
                + MeetingLineHtml("Duration", $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}")
                + MeetingLineHtml("Attendance", $"{presentCount} of {invitedCount} present")
                + MeetingSummaryHtml(summary);
        }

        // Starts with "Meeting Completed" so the ticket thread card shows it as done.
        private static string BuildMeetingSeriesEndedComment(MeetingMaster meeting, DateTime lastDay)
        {
            return MeetingHeadingHtml("Meeting Completed")
                + MeetingLineHtml("Meeting", meeting.title)
                + MeetingLineHtml("Series ended", $"{lastDay:ddd, dd MMM yyyy}")
                + "<p>Each day comment, with its times, attendance and summary, is its own thread.</p>";
        }

        // Meeting comments are HTML like every other thread. Each detail is its own
        // "Label : value" paragraph, which the ticket thread card reads back.
        private static string MeetingHeadingHtml(string heading) =>
            $"<p><strong>{heading}</strong></p>";

        private static string MeetingLineHtml(string label, string? value) =>
            $"<p><strong>{label}</strong> : {WebUtility.HtmlEncode(value ?? "")}</p>";

        private static readonly Regex HtmlTagRegex = new(@"<[a-z][^>]*>", RegexOptions.IgnoreCase);

        // The summary goes in a data-meeting-summary block so the thread card can show
        // it as HTML. The scheduler's editor sends HTML; the ticket thread's form sends
        // plain text, which is encoded with its line breaks kept.
        private static string MeetingSummaryHtml(string? summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return "";

            var body = HtmlTagRegex.IsMatch(summary) ? summary : ToHtmlText(summary);
            return $"<p><strong>Summary</strong></p><div data-meeting-summary=\"\">{body}</div>";
        }

        // days_of_week is a 7-char bitmask, index 0 = Sunday: "0101000" -> "Mon, Wed"
        private static string WeekdayNames(string? daysOfWeek) =>
            string.Join(", ", Enumerable.Range(0, 7)
                .Where(i => daysOfWeek?.ElementAtOrDefault(i) == '1')
                .Select(i => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)i)));



    }
}
