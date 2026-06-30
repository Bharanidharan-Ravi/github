using APIGateWay.Business_Layer.Helper;
using APIGateWay.Business_Layer.Helper.Events.EventFactory;
using APIGateWay.Business_Layer.Helper.Events.Interface;
using APIGateWay.Business_Layer.Interface;
using APIGateWay.Business_Layer.SignalRHub;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.DBContext;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer.DTOs;
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
using static System.Collections.Specialized.BitVector32;

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
        private readonly IEventCenter _eventCenter;

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
            IApiLoggerService apiLogger,
            IEventCenter eventCenter) // <--- ADDED HERE
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
            _apiLogger = apiLogger;
            _eventCenter = eventCenter;
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

            string summary = "Workstream assigned/updated";
            if (response.ThreadCreated) summary = "New comment added to ticket";

            if (response.OldTicketStatus.HasValue && response.NewTicketStatus.HasValue &&
                response.OldTicketStatus != response.NewTicketStatus)
            {
                summary = $"Ticket status changed to {await GetStatusNameAsync(response.NewTicketStatus)}";
            }
            bool notifyClient = dto.toClient ?? false;
            // ── Step 2: thread broadcast (only when a new thread was created) ──
            // UseLastThread=true or pure % update → no new thread → skip
            var action = "Create";
            if (response.ThreadCreated && response.ThreadId.HasValue)
            {
                await BroadcastThreadCreatedAsync(
                    issueId: dto.IssueId,
                    threadId: response.ThreadId.Value,
                    repoId: response.RepoId,
                    notifyClient,
                    action
                );
            }

            // ── Step 3: ticket status broadcast (always fires) ────────────────
            /* await BroadcastTicketStatusAsync(response);

             await BroadcastTicketDetailAsync(dto.IssueId, response.RepoId);*/
            await _eventCenter.PublishAsync<GetTickets>(
                 TicketFactory.TicketUpdated(dto.IssueId, summary, notifyRepo: notifyClient, notifyUsers: true)
             );

            if (dto.TicketOverallPercentage.HasValue || !string.IsNullOrWhiteSpace(dto.TicketStatusSummary))
            {
                // Add dto.TicketStatusSummary here 👇
                await BroadcastTicketProgressAsync(dto.IssueId, response.RepoId, dto.TicketOverallPercentage, dto.TicketStatusSummary, notifyClient);
            }

            return response;
        }


        private async Task LogWorkStreamHistoryAsync(PostWorkStreamDto dto, PostWorkStreamResponse response)
        {
            try
            {
                var actorId = _loginContextService.userId;
                var actorName = _loginContextService.userName;

                // ── 1. LOG GLOBAL TICKET STATUS CHANGES (Close, Reopen, etc.) ──
                if (response.OldTicketStatus.HasValue &&
                    response.NewTicketStatus.HasValue &&
                    response.OldTicketStatus.Value != response.NewTicketStatus.Value)
                {
                    int oldId = response.OldTicketStatus.Value;
                    int newId = response.NewTicketStatus.Value;

                    string oldStatusName = await GetStatusNameAsync(oldId);
                    string newStatusName = await GetStatusNameAsync(newId);

                    // Define your terminal statuses (e.g., Closed, Cancelled)
                    var closedStatusIds = new[] { 14, 15, 16, 17 };

                    bool wasClosed = closedStatusIds.Contains(oldId);
                    bool isNowClosed = closedStatusIds.Contains(newId);

                    if (!wasClosed && isNowClosed)
                    {
                        // 🔥 TICKET CLOSED (Passes ThreadId if they used the comment box!)
                        await _historyRepository.LogAsync(TicketHistoryHelper.TicketClosedWithContext(
                            issueId: dto.IssueId,
                            oldStatusId: oldId,
                            oldStatusName: oldStatusName,
                            threadId: response.ThreadId,
                            newStatusId: newId,
                            newStatusName: newStatusName,
                            actorId: actorId,
                            actorName: actorName));
                    }
                    else if (wasClosed && !isNowClosed)
                    {
                        // 🔥 TICKET REOPENED
                        await _historyRepository.LogAsync(TicketHistoryHelper.TicketReopened(
                            issueId: dto.IssueId,
                            newStatusId: newId,
                            newStatusName: newStatusName,
                            threadId: response.ThreadId,
                            actorId: actorId,
                            actorName: actorName));
                    }
                    else
                    {
                        // 🔥 STANDARD STATUS CHANGE
                        await _historyRepository.LogAsync(TicketHistoryHelper.StatusChanged(
                            issueId: dto.IssueId,
                            oldStatusId: oldId,
                            oldStatusName: oldStatusName,
                            newStatusId: newId,
                            newStatusName: newStatusName,
                            actorId: actorId,
                            actorName: actorName));
                    }
                }

                // ── 2. LOG WORKSTREAM SPECIFIC CHANGES ──
                bool isCompletedByPct = dto.CompletionPct.HasValue && dto.CompletionPct.Value >= 100;
                bool isCompletedByStatus = dto.StreamStatus.HasValue && StatusId.CompletedStatuses.Contains(dto.StreamStatus.Value);
                bool isRoutingToOthers = dto.NextAssignees != null && dto.NextAssignees.Any();

                if (isCompletedByPct || isCompletedByStatus)
                {
                    if (response.NewTicketStatus != 14 && response.NewTicketStatus != 15 && response.NewTicketStatus != 16)
                    {
                        var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
                        await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCompleted(
                            issueId: dto.IssueId,
                            assigneeName: assigneeName,
                            streamName: response.StreamName ?? dto.StreamName ?? "General",
                            NewValue: response.NewTicketStatus.ToString(),
                            workStreamId: response.WorkStreamId,
                            actorId: actorId,
                            actorName: actorName,
                            threadId: response.ThreadId,
                            oldValue: response.OldTicketStatus.ToString()
                        ));
                    }

                    if (!isRoutingToOthers) return;
                }

                // ── SCENARIO 1: SELF-ASSIGNMENT ──
                if (!dto.AssignOnly && !isCompletedByPct && !isCompletedByStatus && !isRoutingToOthers)
                {
                    var stream = await _db.WorkStreams
                        .Where(ws => ws.StreamId == response.WorkStreamId)
                        .Select(ws => new { ws.ParentThreadId })
                        .FirstOrDefaultAsync();

                    bool isNewRow = stream != null && stream.ParentThreadId == response.ThreadId;

                    if (isNewRow)
                    {
                        var assigneeName = await GetEmployeeNameAsync(response.ResourceId);
                        await _historyRepository.LogAsync(TicketHistoryHelper.WorkStreamCreated(
                            issueId: dto.IssueId,
                            assigneeName: assigneeName,
                            streamName: response.StreamName ?? dto.StreamName ?? "General",
                            statusName: await GetStatusNameAsync(dto.StreamStatus),
                            workStreamId: response.WorkStreamId,
                            actorId: actorId,
                            actorName: actorName,
                            threadId: response.ThreadId
                        ));
                    }
                }

                // ── SCENARIO 2: HANDOFF ──
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
                            assigneeName: assigneeName,
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
        public async Task BroadcastThreadCreatedAsync(
            Guid issueId,
            long threadId,
            Guid? repoId,
            bool notifyClient,
            string? action)
        {
            if (string.IsNullOrEmpty(repoId.ToString())) return;

            // Fetch rich thread data via SP — same SP used by ThreadRepo
            ThreadList? freshThread = null;
            try
            {
                var syncParams = new Dictionary<string, string>
                {
                    { "IssuesId", issueId.ToString() },
                    { "Role", _loginContextService.role.ToString() }
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
                    Action = action,
                    Payload = freshThread,     // rich SP data with all joined fields
                    KeyField = "ThreadId",
                    IssueId = issueId,
                    RepoKey = notifyClient ? repoId.ToString() : null,
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
                    Entity = "Ticket",
                    Action = "Update",
                    Payload = response.BroadcastPayload,  // pre-built by service
                    KeyField = "Issue_Id",
                    IssueId = response.IssueId,
                    RepoKey = response.RepoId.ToString(),
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

        private async Task BroadcastTicketDetailAsync(Guid issueId, Guid? repoKey)
        {
            //if (string.IsNullOrEmpty(repoKey)) return;
            GetTickets? richTicketData = null;

            try
            {
                richTicketData = await _syncExecutionService.FetchRichDataAsync<GetTickets>(
                    configKey: "TicketsList",
                    syncParams: new Dictionary<string, string>
                    {
                        { "IssueId", issueId.ToString() },
                        { "Role", _loginContextService.role.ToString() }
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
                    RepoKey = richTicketData.RepoKey ?? repoKey.ToString(),
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

        private async Task BroadcastTicketProgressAsync(
            Guid issueId,
            Guid? repoId,
            decimal? overallPct,
            string? statusSummary,
            bool notifyClient)
        {
            if (repoId == null) return;

            TicketProgressLogDto? latestProgress = null;

            try
            {
                var syncParams = new Dictionary<string, string>
        {
            { "IssueId", issueId.ToString() }
        };

                var syncResponse =
                    await _syncExecutionService.ExecuteLocalAsync<TicketProgressLogDto>(
                        databaseName: "",
                        storedProcedure: "GetTicketProgressLogsByIssueId",
                        lastSync: null,
                        parameters: syncParams,
                        source: "WorkStreamRepo"
                    );

                if (syncResponse.Ok && syncResponse.Data != null)
                {
                    var logs = syncResponse.Data as IEnumerable<TicketProgressLogDto>;

                    // fallback for JsonElement
                    if (logs == null &&
                        syncResponse.Data is System.Text.Json.JsonElement jsonElement)
                    {
                        logs =
                            System.Text.Json.JsonSerializer.Deserialize<List<TicketProgressLogDto>>(
                                jsonElement.GetRawText(),
                                new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });
                    }

                    // newest row
                    latestProgress = logs?
                        .OrderByDescending(x => x.CreatedAt)
                        .FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamRepo] TicketProgress fetch failed: {ex.Message}");
                return;
            }

            if (latestProgress == null) return;

            try
            {
                await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                {
                    Entity = "TicketProgress",

                    // IMPORTANT
                    Action = RealtimeActions.Create,

                    Payload = latestProgress,

                    // IMPORTANT
                    KeyField = "LogId",
                    RepoKey = notifyClient ? repoId.ToString() : null,
                    IssueId = issueId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamRepo] TicketProgress broadcast failed: {ex.Message}");
            }
        }
    }
}