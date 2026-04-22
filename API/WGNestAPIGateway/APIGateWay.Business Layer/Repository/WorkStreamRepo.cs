using APIGateWay.Business_Layer.Helper;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Repository
{
    public class WorkStreamRepo : IWorkStreamRepo
    {
        private readonly IDomainService _domainService;
        private readonly ILoginContextService _loginContextService;
        private readonly APIGateWayCommonService _commonService;
        private readonly APIGatewayDBContext _db;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly IWorkStreamService _workStream;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly ITicketHistoryRepository _historyRepository;
        private readonly IApiLoggerService _apiLogger;

        // Update your constructor to include IApiLoggerService
        public WorkStreamRepo(
            IDomainService domainService,
            ILoginContextService loginContext,
            APIGateWayCommonService aPIGateWay,
            APIGatewayDBContext aPIGatewayDB,
            IHelperGetData helperGet,
            IRealtimeNotifier realtimeNotifier,
            IWorkStreamService workStream,
            ISyncExecutionService syncExecutionService,
            ITicketHistoryRepository historyRepository,
            IApiLoggerService apiLogger) // <--- ADDED HERE
        {
            _domainService = domainService;
            _loginContextService = loginContext;
            _commonService = aPIGateWay;
            _db = aPIGatewayDB;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _workStream = workStream;
            _syncExecutionService = syncExecutionService;
            _historyRepository = historyRepository;
            _apiLogger = apiLogger; // <--- ADDED HERE
        }


        // =====================================================================
        // INDIVIDUAL STREAM POST — POST /api/workstream
        //
        // UI sends: StreamName, StreamStatus (StatusId int), UseLastThread toggle
        // Toggle ON  → link last thread of this user
        // Toggle OFF → create new ThreadMaster row from Comment
        // =====================================================================
        public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        {
            // ── Step 1: domain logic (DB writes, status compute) ──────────────
            var response = await _workStream.PostWorkStreamAsync(dto);
            await LogWorkStreamHistoryAsync(dto, response);

            // ── Step 2: thread broadcast (only when a new thread was created) ──
            // UseLastThread=true or pure % update → no new thread → skip
            if (response.ThreadCreated && response.ThreadId.HasValue)
            {
                await BroadcastThreadCreatedAsync(
                    issueId: dto.IssueId,
                    threadId: response.ThreadId.Value,
                    repoKey: response.RepoKey
                );
            }

            // ── Step 3: ticket status broadcast (always fires) ────────────────
            await BroadcastTicketStatusAsync(response);

            await BroadcastTicketDetailAsync(dto.IssueId, response.RepoKey);

            if (dto.TicketOverallPercentage.HasValue || !string.IsNullOrWhiteSpace(dto.TicketStatusSummary))
            {
                // Add dto.TicketStatusSummary here 👇
                await BroadcastTicketProgressAsync(dto.IssueId, response.RepoKey, response.TicketOverallPct, dto.TicketStatusSummary);
            }

            return response;
        }


        private async Task LogWorkStreamHistoryAsync(PostWorkStreamDto dto, PostWorkStreamResponse response)
        {
            try
            {
                var actorId = _loginContextService.userId;
                var actorName = _loginContextService.userName;

                bool isCompletedByPct = dto.CompletionPct.HasValue && dto.CompletionPct.Value >= 100;
                bool isCompletedByStatus = dto.StreamStatus.HasValue && StatusId.CompletedStatuses.Contains(dto.StreamStatus.Value);

                // Check if this action includes handing off to other people
                bool isRoutingToOthers = dto.NextAssignees != null && dto.NextAssignees.Any();

                if (isCompletedByPct || isCompletedByStatus)
                {
                    var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
                    await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCompleted(
                        issueId: dto.IssueId,
                        assigneeName: assigneeName,
                        streamName: response.StreamName ?? dto.StreamName ?? "General",
                        workStreamId: response.WorkStreamId,
                        actorId: actorId,
                        actorName: actorName,
                        threadId: response.ThreadId
                    ));

                    if (!isRoutingToOthers)
                        return;
                }

                // ── SCENARIO 1: SELF-ASSIGNMENT (Working on the ticket) ──
                if (!dto.AssignOnly && !isCompletedByPct && !isCompletedByStatus && !isRoutingToOthers)
                {
                    // 🔥 THE FIX: Stop using the 5-second timer. 
                    // Instead, check if this EXACT thread is the parent of the WorkStream.
                    var stream = await _db.WorkStreams
                        .Where(ws => ws.StreamId == response.WorkStreamId)
                        .Select(ws => new { ws.ParentThreadId })
                        .FirstOrDefaultAsync();

                    // If the stream's ParentThreadId matches the thread we just created, 
                    // it is 100% a brand new stream. It will only ever equal true ONCE.
                    bool isNewRow = stream != null && stream.ParentThreadId == response.ThreadId;

                    if (isNewRow)
                    {
                        var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
                        await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
                            issueId: dto.IssueId,
                            assigneeName: assigneeName, // This is the current user
                            streamName: response.StreamName ?? dto.StreamName ?? "General",
                            statusName: await GetStatusNameAsync(dto.StreamStatus),
                            workStreamId: response.WorkStreamId,
                            actorId: actorId,
                            actorName: actorName,
                            threadId: response.ThreadId
                        ));
                    }
                }

                // ── SCENARIO 2: HANDOFF (Routing to someone else) ──
                if (isRoutingToOthers)
                {
                    var assigneeIds = dto.NextAssignees.Select(a => a.Id).ToList();
                    var employeeNameList = await _db.eMPLOYEEMASTERs
                        .Where(e => assigneeIds.Contains(e.EmployeeID))
                        .Select(e => new { e.EmployeeID, Name = e.EmployeeName ?? "Unknown" })
                        .ToListAsync();

                    var employeeNames = employeeNameList.ToDictionary(e => e.EmployeeID, e => e.Name);

                    foreach (var assignee in dto.NextAssignees)
                    {
                        // 🔥 EXTRA SAFETY: If they somehow bypassed the UI and assigned themselves, 
                        // skip it here so we don't get duplicates.
                        if (string.Equals(assignee.Id.ToString(), actorId.ToString(), StringComparison.OrdinalIgnoreCase)) continue;

                        var assigneeName = employeeNames.GetValueOrDefault(assignee.Id, "Unknown");

                        var assigneeStream = await _db.WorkStreams
                            .Where(ws => ws.IssueId == dto.IssueId && ws.ResourceId == assignee.Id)
                         .OrderByDescending(ws => ws.CreatedAt)
                         .Select(ws => new { ws.StreamId })
                         .FirstOrDefaultAsync();

                        if (assigneeStream == null) continue;

                        await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
                            issueId: dto.IssueId,
                            assigneeName: assigneeName, // This is the target user
                            streamName: response.StreamName ?? dto.StreamName ?? "General",
                            statusName: await GetStatusNameAsync(assignee.StreamId),
                            workStreamId: assigneeStream.StreamId,
                            actorId: actorId,
                            actorName: actorName,
                            threadId: response.ThreadId
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkStreamRepo] History logging failed: {ex.Message}");
            }
        }

        //private async Task LogWorkStreamHistoryAsync(PostWorkStreamDto dto, PostWorkStreamResponse response)
        //{
        //    try
        //    {
        //        var actorId = _loginContextService.userId;
        //        var actorName = _loginContextService.userName;

        //        bool isCompletedByPct = dto.CompletionPct.HasValue && dto.CompletionPct.Value >= 100;
        //        bool isCompletedByStatus = dto.StreamStatus.HasValue && StatusId.CompletedStatuses.Contains(dto.StreamStatus.Value);

        //        // 🔥 FIX 1: Check if this action includes handing off to other people
        //        bool isRoutingToOthers = dto.NextAssignees != null && dto.NextAssignees.Any();

        //        if (isCompletedByPct || isCompletedByStatus)
        //        {
        //            var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
        //            await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCompleted(
        //                issueId: dto.IssueId,
        //                assigneeName: assigneeName,
        //                streamName: response.StreamName ?? dto.StreamName ?? "General",
        //                workStreamId: response.WorkStreamId,
        //                actorId: actorId,
        //                actorName: actorName,
        //                threadId: response.ThreadId
        //                ));

        //            if (!isRoutingToOthers)
        //                return;
        //        }

        //        // 🔥 FIX 2: Skip logging "self-assignment" if the user is routing the ticket to someone else.
        //        // We added "&& !isRoutingToOthers" to this if statement.
        //        // This stops "AnbuMani assigned AnbuMani" from printing when you assign Sandhiya.
        //        if (!dto.AssignOnly && !isCompletedByPct && !isCompletedByStatus && !isRoutingToOthers)
        //        {
        //            var stream = await _db.WorkStreams
        //                .Where(ws => ws.StreamId == response.WorkStreamId)
        //                .Select(ws => new { ws.CreatedAt })
        //                .FirstOrDefaultAsync();

        //            bool isNewRow = stream?.CreatedAt.HasValue == true && (DateTime.UtcNow - stream.CreatedAt.Value).TotalSeconds < 5;

        //            if (isNewRow)
        //            {
        //                var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
        //                await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
        //                    issueId: dto.IssueId,
        //                    assigneeName: assigneeName, // This is the current user
        //                    streamName: response.StreamName ?? dto.StreamName ?? "General",
        //                    statusName: await GetStatusNameAsync(dto.StreamStatus),
        //                    workStreamId: response.WorkStreamId,
        //                    actorId: actorId,
        //                    actorName: actorName,
        //                    threadId: response.ThreadId
        //                ));
        //            }
        //        }

        //        // Event: User assigns another user
        //        if (isRoutingToOthers)
        //        {
        //            var assigneeIds = dto.NextAssignees.Select(a => a.Id).ToList();
        //            var employeeNameList = await _db.eMPLOYEEMASTERs
        //                .Where(e => assigneeIds.Contains(e.EmployeeID))
        //                .Select(e => new { e.EmployeeID, Name = e.EmployeeName ?? "Unknown" })
        //                .ToListAsync();

        //            var employeeNames = employeeNameList
        //                .ToDictionary(e => e.EmployeeID, e => e.Name);

        //            foreach (var assignee in dto.NextAssignees)
        //            {
        //                var assigneeName = employeeNames.GetValueOrDefault(assignee.Id, "Unknown");

        //                var assigneeStream = await _db.WorkStreams
        //                    .Where(ws =>
        //                    ws.IssueId == dto.IssueId &&
        //                    ws.ResourceId == assignee.Id)
        //                 .OrderByDescending(ws => ws.CreatedAt)
        //                 .Select(ws => new { ws.StreamId })
        //                 .FirstOrDefaultAsync();

        //                if (assigneeStream == null) continue;

        //                await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
        //                    issueId: dto.IssueId,
        //                    assigneeName: assigneeName, // This is the target user (e.g., Sandhiya)
        //                    streamName: response.StreamName ?? dto.StreamName ?? "General",
        //                    statusName: await GetStatusNameAsync(assignee.StreamId),
        //                    workStreamId: assigneeStream.StreamId,
        //                    actorId: actorId,
        //                    actorName: actorName,
        //                    threadId: response.ThreadId
        //                ));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[WorkStreamRepo] History logging failed: {ex.Message}");
        //    }
        //}
        private async Task<string> GetEmployeeNameAsync(Guid resourceId)
        {
            var emp = await _db.eMPLOYEEMASTERs
                .Where(e => e.EmployeeID == resourceId)
                .Select(e => new { Name = e.EmployeeName ?? "Unknown" })
                .FirstOrDefaultAsync();
            return emp?.Name ?? "Unknown";
        }


        private async Task<string> GetStatusNameAsync(int? statusId)
        {
            if (statusId == null) return "None";

            var statusName = await _db.StatusMasters
                .Where(s => s.Status_Id == statusId)
                .Select(s => s.Status_Name)
                .FirstOrDefaultAsync();

            return statusName ?? "Unknown";
        }

        // =====================================================================
        // THREAD BROADCAST
        //
        // Fetches rich thread data from GETTHREADLIST SP — same as original
        // ThreadRepo.CreateThreadAsync pattern.
        // Broadcasts ThreadsList → Create so all clients see the new comment.
        // =====================================================================
        private async Task BroadcastThreadCreatedAsync(
            Guid issueId,
            long threadId,
            string repoKey)
        {
            if (string.IsNullOrEmpty(repoKey)) return;

            // Fetch rich thread data via SP — same SP used by ThreadRepo
            ThreadList? freshThread = null;
            try
            {
                var syncParams = new Dictionary<string, string>
                {
                    { "IssuesId", issueId.ToString() }
                };

                var syncResponse = await _syncExecutionService.ExecuteLocalAsync<ThreadList>(
                    databaseName: "",
                    storedProcedure: "GETTHREADLIST",
                    lastSync: null,
                    parameters: syncParams,
                    source: "WorkStreamRepo"
                );

                if (syncResponse.Ok && syncResponse.Data != null)
                {
                    var threads = syncResponse.Data as IEnumerable<ThreadList>;

                    // Fallback: data layer returns JsonElement instead of typed list
                    if (threads == null &&
                        syncResponse.Data is System.Text.Json.JsonElement jsonElement)
                    {
                        threads = System.Text.Json.JsonSerializer.Deserialize<List<ThreadList>>(
                            jsonElement.GetRawText(),
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                    }

                    // Find the exact thread we just inserted by its sequence ID
                    freshThread = threads?.FirstOrDefault(t => t.ThreadId == threadId);
                }
            }
            catch (Exception ex)
            {
                // SP fetch failure must never break the response
                // Thread is already saved in DB — just log and skip the broadcast
                Console.WriteLine(
                    $"[WorkStreamRepo] GETTHREADLIST fetch failed for thread {threadId}: {ex.Message}");
                return;
            }

            if (freshThread == null) return;

            try
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "ThreadsList",
                    Action = "Create",
                    Payload = freshThread,     // rich SP data with all joined fields
                    KeyField = "ThreadId",
                    IssueId = issueId,
                    RepoKey = repoKey,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamRepo] ThreadsList broadcast failed: {ex.Message}");
            }
        }

        // =====================================================================
        // TICKET STATUS BROADCAST
        //
        // Uses pre-built payload from WorkStreamService — no extra DB call.
        // Skips if ticket is already terminal (Closed/Cancelled).
        // =====================================================================
        private async Task BroadcastTicketStatusAsync(PostWorkStreamResponse response)
        {
            // Service sets IsTerminal=true when ticket was already Closed/Cancelled
            // No point broadcasting a status update for a terminal ticket
            if (response.IsTerminal) return;

            if (string.IsNullOrEmpty(response.RepoKey)) return;

            if (response.BroadcastPayload == null) return;

            try
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "TicketsList",
                    Action = "StatusUpdate",
                    Payload = response.BroadcastPayload,  // pre-built by service
                    KeyField = "Issue_Id",
                    IssueId = response.IssueId,
                    RepoKey = response.RepoKey,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                // SignalR failure must never break the API response
                Console.WriteLine(
                    $"[WorkStreamRepo] TicketsList broadcast failed: {ex.Message}");
            }
        }

        private async Task BroadcastTicketDetailAsync(Guid issueId, string repoKey)
        {
            if (string.IsNullOrEmpty(repoKey)) return;

            GetTickets? richTicketData = null;

            try
            {
                richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
                    configKey: "TicketsList",
                    syncParams: new Dictionary<string, string>
                    {
                { "IssueId", issueId.ToString() }
                    },
                    matchPredicate: p => p.Issue_Id == issueId,
                    fallbackData: null,  // null = don't broadcast if SP fails
                    lastSync: null
                );
            }
            catch (Exception ex)
            {
                // SP fetch failure must never break the response
                // Ticket is already updated in DB — just log and skip broadcast
                Console.WriteLine(
                    $"[WorkStreamRepo] FetchRichDataAsync failed for ticket {issueId}: {ex.Message}");
                return;
            }

            if (richTicketData == null) return;

            try
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "Ticket",          // ticket DETAIL screen listens to this
                    Action = "Update",          // same action as TicketRepo.UpdateTicketAsync
                    Payload = richTicketData,    // full rich data — workstreams, labels, assignees
                    KeyField = "Issue_Id",
                    IssueId = issueId,
                    RepoKey = richTicketData.RepoKey ?? repoKey,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamRepo] Ticket detail broadcast failed for {issueId}: {ex.Message}");
            }
        }

        // =====================================================================
        // TICKET PROGRESS BROADCAST
        //
        // Broadcasts when a TicketProgressLog is explicitly inserted or updated.
        // =====================================================================
        private async Task BroadcastTicketProgressAsync(Guid issueId, string repoKey, decimal? newPercentage, string statusSummary) // 👈 Added parameter
        {
            if (string.IsNullOrEmpty(repoKey)) return;

            try
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "TicketProgress",
                    Action = "Update",
                    Payload = new
                    {
                        IssueId = issueId,
                        Percentage = newPercentage,
                        Summary = statusSummary, // 👈 Added to payload
                        UpdatedAt = DateTime.UtcNow
                    },
                    KeyField = "IssueId",
                    IssueId = issueId,
                    RepoKey = repoKey,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkStreamRepo] TicketProgress broadcast failed for {issueId}: {ex.Message}");
            }
        }

    }
}



////// OLD CODE ----------/////////////////////

//public async Task<bool> CheckAndCompleteTicketAsync(Guid issueId)
//{
//    // ── Step 1: load all active (non-inactive) subtasks for this ticket ───────
//    // "Inactive" means removed from ticket — they never block or count
//    // StatusId.IsInactive covers StatusId.Inactive (20) and StatusId.Cancelled (19)
//    var activeSubtasks = await _db.WorkStreams
//        .Where(ws =>
//            ws.IssueId == issueId &&
//            ws.StreamStatus != StatusId.Inactive)
//        .ToListAsync();

//    // ── Step 2: no subtasks at all → nothing to evaluate ─────────────────────
//    if (!activeSubtasks.Any())
//        return false;

//    // ── Step 3: check if every subtask is in a "done" status ─────────────────
//    // StatusId.IsCompleted checks against CompletedStatuses:
//    //   { 6=DevCompleted, 12=FuncFixCompleted, 13=TransportCreated,
//    //     14=TransportReleased, 15=MovedToQA, 16=MovedToProduction, 18=Closed }
//    bool allDone = activeSubtasks.All(ws =>
//        StatusId.IsCompleted(ws.StreamStatus ?? 0));

//    if (!allDone)
//        return false;

//    // ── Step 4: load ticket — need Status, Repo_Id, Issue_Id for update + broadcast
//    var ticket = await _db.Set<TicketMaster>()
//        .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

//    if (ticket == null)
//        return false;

//    // ── Step 5: skip if already closed or cancelled ───────────────────────────
//    // Never downgrade or re-trigger completed state
//    if (ticket.Status == StatusId.Closed ||
//        ticket.Status == StatusId.Cancelled)
//        return false;

//    // ── Step 6: update ticket status to Closed (18) ──────────────────────────
//    // Use UpdateTrackedEntityAsync — respects EF audit (UpdatedAt, UpdatedBy)
//    await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
//        t => t.Issue_Id == issueId,
//        t =>
//        {
//            t.Status = StatusId.Closed;   // 18 = CLS
//        }
//    );

//    // ── Step 7: get RepoKey for SignalR broadcast ─────────────────────────────
//    // Needed to route the broadcast to the correct repo room
//    string repoKey = string.Empty;
//    try
//    {
//        if (ticket.RepoId.HasValue)
//            repoKey = await _helperGet.GetRepoKeyByIdAsync(ticket.RepoId);
//    }
//    catch
//    {
//        // RepoKey lookup failed — continue without broadcast
//        // Ticket is already closed in DB — don't rollback for a SignalR failure
//    }

//    // ── Step 8: broadcast ticket auto-completion via SignalR ──────────────────
//    // Tells all connected clients in this repo that the ticket just completed
//    // UI can then update the ticket card status in real time
//    if (!string.IsNullOrEmpty(repoKey))
//    {
//        try
//        {
//            // Build a minimal payload — clients only need Status + Issue_Id to update the card
//            var completionPayload = new
//            {
//                Issue_Id = issueId,
//                Status = StatusId.Closed,
//                StatusName = "Closed",
//                ClosedAt = DateTime.UtcNow,
//                AutoClosed = true,          // flag so UI can show "auto-completed" toast
//            };

//            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//            {
//                Entity = "TicketsList",
//                Action = "StatusUpdate",   // same action as manual status update
//                Payload = completionPayload,
//                KeyField = "Issue_Id",
//                IssueId = issueId,
//                RepoKey = repoKey,
//                Timestamp = DateTime.UtcNow
//            });
//        }
//        catch (Exception ex)
//        {
//            // SignalR failure must never break the response
//            // Ticket is already closed — just log and continue
//            Console.WriteLine(
//                $"[WorkStreamService] SignalR broadcast failed for ticket {issueId}: {ex.Message}");
//        }
//    }

//    return true;  // ticket was just auto-completed
//}
//public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid issueId)
//{
//    // ── Step 1: load all non-inactive subtasks with their status Sort_Order ───
//    // Join with Status_Master to get Sort_Order for stage comparison
//    var subtasks = await _db.WorkStreams
//        .Where(ws =>
//            ws.IssueId == issueId &&
//            ws.StreamStatus != null &&
//            ws.StreamStatus != StatusId.Inactive &&
//            ws.StreamStatus != StatusId.Cancelled)
//        .Join(_db.StatusMasters,
//            ws => ws.StreamStatus,
//            sm => sm.Status_Id,
//            (ws, sm) => new
//            {
//                ws.StreamStatus,
//                ws.CompletionPct,
//                sm.Sort_Order,
//                sm.Status_Name,
//                IsCompleted = StatusId.CompletedStatuses.Contains(ws.StreamStatus!.Value),
//            })
//        .ToListAsync();

//    // ── Step 2: no subtasks → nothing to compute ─────────────────────────────
//    if (!subtasks.Any())
//    {
//        return new TicketStatusResult
//        {
//            ComputedStatusId = StatusId.New,
//            ComputedStatusName = "New",
//            OverallPct = 0,
//            TotalSubtasks = 0,
//            CompletedSubtasks = 0,
//            ActiveSubtasks = 0,
//            TicketAutoCompleted = false,
//        };
//    }

//    // ── Step 3: compute OverallPct ────────────────────────────────────────────
//    // Simple average of all non-inactive CompletionPct values
//    // Treat null CompletionPct as 0
//    var overallPct = Math.Round(
//        subtasks.Average(s => (double)(s.CompletionPct ?? 0)),
//        2);

//    var totalSubtasks = subtasks?.Count;
//    var completedSubtasks = subtasks.Count(s => s.IsCompleted);
//    var activeSubtasks = subtasks.Count(s => !s.IsCompleted);

//    // ── Step 4: determine computed status ────────────────────────────────────
//    // Check if ALL are completed first
//    bool allCompleted = subtasks.All(s => s.IsCompleted);

//    int computedStatusId;
//    string computedStatusName;

//    if (allCompleted)
//    {
//        // Every subtask is done → auto-close
//        computedStatusId = StatusId.Closed;
//        computedStatusName = "Closed";
//    }
//    else
//    {
//        // Find the ACTIVE subtask with the HIGHEST Sort_Order
//        // "Highest Sort_Order" = most advanced stage in the pipeline
//        // This means: if anyone is in Testing (Sort=8) while others are
//        // still in Development (Sort=5), the ticket reflects Testing
//        var mostAdvancedActive = subtasks
//            .Where(s => !s.IsCompleted)      // only active ones
//            .OrderByDescending(s => s.Sort_Order)
//            .First();

//        computedStatusId = mostAdvancedActive.StreamStatus!.Value;
//        computedStatusName = mostAdvancedActive.Status_Name;
//    }

//    // ── Step 5: load current ticket ──────────────────────────────────────────
//    var ticket = await _db.Set<TicketMaster>()
//        .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

//    if (ticket == null)
//    {
//        return new TicketStatusResult
//        {
//            ComputedStatusId = computedStatusId,
//            ComputedStatusName = computedStatusName,
//            OverallPct = (decimal)overallPct,
//            TotalSubtasks = totalSubtasks,
//            CompletedSubtasks = completedSubtasks,
//            ActiveSubtasks = activeSubtasks,
//            TicketAutoCompleted = allCompleted,
//        };
//    }

//    // ── Step 6: skip update if ticket is already terminal ────────────────────
//    bool isAlreadyTerminal =
//        ticket.Status == StatusId.Closed ||
//        ticket.Status == StatusId.Cancelled;

//    if (!isAlreadyTerminal)
//    {
//        // Update Status + OverallPct in one call
//        await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
//            t => t.Issue_Id == issueId,
//            t =>
//            {
//                t.Status = computedStatusId;
//                t.CompletionPct = (decimal?)overallPct;
//                t.StatusName = computedStatusName;
//                // UpdatedAt, UpdatedBy → DBContext audit
//            }
//        );
//    }

//    // ── Step 7: get RepoKey for broadcast ────────────────────────────────────
//    string repoKey = string.Empty;
//    try
//    {
//        if (ticket.RepoId.HasValue)
//            repoKey = await _helperGet.GetRepoKeyByIdAsync(ticket.RepoId.Value);
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine(
//            $"[WorkStreamService] RepoKey lookup failed for {issueId}: {ex.Message}");
//    }

//    // ── Step 8: broadcast live status update ─────────────────────────────────
//    // Fires on EVERY subtask change — not just on completion
//    // UI receives this and updates the ticket card status + progress bar in real time
//    if (!string.IsNullOrEmpty(repoKey) && !isAlreadyTerminal)
//    {
//        try
//        {
//            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
//            {
//                Entity = "TicketsList",
//                Action = "StatusUpdate",
//                Payload = new
//                {
//                    Issue_Id = issueId,
//                    Status = computedStatusId,
//                    StatusName = computedStatusName,
//                    OverallPct = overallPct,
//                    TotalSubtasks = totalSubtasks,
//                    CompletedSubtasks = completedSubtasks,
//                    ActiveSubtasks = activeSubtasks,
//                    AutoClosed = allCompleted,
//                    UpdatedAt = DateTime.UtcNow,
//                },
//                KeyField = "Issue_Id",
//                IssueId = issueId,
//                RepoKey = repoKey,
//                Timestamp = DateTime.UtcNow
//            });
//        }
//        catch (Exception ex)
//        {
//            // Never break the response for a SignalR failure
//            Console.WriteLine(
//                $"[WorkStreamService] Broadcast failed for {issueId}: {ex.Message}");
//        }
//    }

//    return new TicketStatusResult
//    {
//        ComputedStatusId = computedStatusId,
//        ComputedStatusName = computedStatusName,
//        OverallPct = (decimal)overallPct,
//        TotalSubtasks = totalSubtasks,
//        CompletedSubtasks = completedSubtasks,
//        ActiveSubtasks = activeSubtasks,
//        TicketAutoCompleted = allCompleted,
//    };
//}