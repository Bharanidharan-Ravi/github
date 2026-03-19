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

        public WorkStreamRepo(IDomainService domainService, ILoginContextService loginContext
            , APIGateWayCommonService aPIGateWay
            , APIGatewayDBContext aPIGatewayDB
            , IHelperGetData helperGet
            , IRealtimeNotifier realtimeNotifier, IWorkStreamService workStream, ISyncExecutionService syncExecutionService)
        {
            _domainService = domainService;
            _loginContextService = loginContext;
            _commonService = aPIGateWay;
            _db = aPIGatewayDB;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _workStream = workStream;
            _syncExecutionService = syncExecutionService;
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

            return response;
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
        //public async Task<PostWorkStreamResponse> PostWorkStreamAsync(PostWorkStreamDto dto)
        //{
        //    PostWorkStreamResponse response = null;

        //    await _domainService.ExecuteInTransactionAsync(async () =>
        //    {
        //        // ── Step 1: resolve who this is for ───────────────────────────
        //        var resourceId = dto.ResourceId ?? _loginContextService.userId;

        //        // ── Step 2: resolve ThreadId from toggle ──────────────────────
        //        int threadId = 0;
        //        bool threadCreated = false;

        //        if (dto.UseLastThread)
        //        {
        //            // Find last thread this user posted for this ticket
        //            var lastThread = await _db.ISSUETHREADS
        //                .Where(t =>
        //                    t.Issue_Id == dto.IssueId &&
        //                    t.CreatedBy == resourceId)
        //                .OrderByDescending(t => t.ThreadId)
        //                .FirstOrDefaultAsync();

        //            if (lastThread == null)
        //                throw new InvalidOperationException(
        //                    "No previous thread found for this user on this ticket. " +
        //                    "Please add a comment instead (toggle off).");

        //            threadId = lastThread.ThreadId;
        //        }
        //        else
        //        {
        //            if (string.IsNullOrWhiteSpace(dto.Comment))
        //                throw new InvalidOperationException(
        //                    "A comment is required when not using the last thread.");

        //            var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");

        //            var newThread = new ThreadMaster
        //            {
        //                ThreadId = seq.CurrentValue,
        //                Issue_Id = dto.IssueId,
        //                CommentText = dto.Comment,
        //                HtmlDesc = dto.Comment,
        //            };

        //            await _domainService.SaveEntityWithAttachmentsAsync(newThread, null);
        //            threadId = seq.CurrentValue;
        //            threadCreated = true;
        //        }

        //        // ── Step 3: upsert subtask row ────────────────────────────────
        //        var existingRow = await _db.WorkStreams
        //        .FirstOrDefaultAsync(ws =>
        //            ws.IssueId == dto.IssueId &&
        //            ws.ResourceId == resourceId &&
        //            ws.StreamStatus != null &&
        //            ws.StreamStatus != StatusId.Inactive);

        //        var streamName = await _workStream.GetDepartmentNameAsync(resourceId);
        //        Guid workStreamId = Guid.Empty;
        //        long? parentThreadId = null;

        //        if (existingRow != null)
        //        {
        //            await _domainService.UpdateTrackedEntityAsync<WorkStream>(
        //                ws => ws.StreamId == existingRow.StreamId,
        //                ws =>
        //                {
        //                    //ws.StreamName = streamName;
        //                    ws.StreamStatus = dto.StreamStatus ?? ws.StreamStatus;

        //                    // 👇 This is already perfectly written!
        //                    ws.CompletionPct = dto.CompletionPct ?? ws.CompletionPct;
        //                    ws.ThreadId = threadId;

        //                    //if (dto.ParentThreadId.HasValue && ws.ParentThreadId == null)
        //                    //    ws.ParentThreadId = dto.ParentThreadId;

        //                    if (dto.TargetDate.HasValue)
        //                        ws.TargetDate = dto.TargetDate;
        //                }
        //            );

        //            workStreamId = existingRow.StreamId;
        //            parentThreadId = existingRow.ParentThreadId ?? dto.ParentThreadId;
        //        }
        //        else
        //        {
        //            var newRow = new WorkStream
        //            {
        //                IssueId = dto.IssueId,
        //                StreamName = streamName,
        //                ResourceId = resourceId,
        //                StreamStatus = dto.StreamStatus,
        //                CompletionPct = dto.CompletionPct ?? 0,
        //                TargetDate = dto.TargetDate,
        //                ThreadId = threadId,
        //                ParentThreadId = threadId,
        //            };

        //            await _domainService.SaveEntityWithAttachmentsAsync(newRow, null);
        //            workStreamId = newRow.StreamId;
        //            parentThreadId = newRow.ParentThreadId;
        //        }

        //        // ── Step 4: check ticket completion ───────────────────────────
        //        //var ticketCompleted = await CheckAndCompleteTicketAsync(dto.IssueId);
        //        var statusResult = await ComputeAndUpdateTicketStatusAsync(dto.IssueId);
        //        var ticketCompleted = statusResult.TicketAutoCompleted;

        //        // ── Step 5: get status name for response ──────────────────────
        //        //var statusName = await GetStatusNameAsync(dto.StreamStatus); 
        //        var finalStreamStatus = dto.StreamStatus ?? existingRow?.StreamStatus;
        //        var statusName = finalStreamStatus.HasValue ? await GetStatusNameAsync(finalStreamStatus.Value) : null;

        //        response = new PostWorkStreamResponse
        //        {
        //            WorkStreamId = workStreamId,
        //            ThreadId = threadId,
        //            ParentThreadId = parentThreadId,
        //            StreamName = streamName,
        //            StreamStatus = finalStreamStatus,
        //            StatusName = statusName,
        //            ThreadCreated = threadCreated,
        //            TicketCompleted = ticketCompleted,
        //            TicketStatusId = statusResult.ComputedStatusId,
        //            TicketStatusName = statusResult.ComputedStatusName,
        //            TicketOverallPct = statusResult.OverallPct,
        //            TotalSubtasks = statusResult.TotalSubtasks,
        //            CompletedSubtasks = statusResult.CompletedSubtasks,
        //            ActiveSubtasks = statusResult.ActiveSubtasks,
        //        };

        //        return true;
        //    });

        //    return response!;
        //}

        public async Task<bool> CheckAndCompleteTicketAsync(Guid issueId)
        {
            // ── Step 1: load all active (non-inactive) subtasks for this ticket ───────
            // "Inactive" means removed from ticket — they never block or count
            // StatusId.IsInactive covers StatusId.Inactive (20) and StatusId.Cancelled (19)
            var activeSubtasks = await _db.WorkStreams
                .Where(ws =>
                    ws.IssueId == issueId &&
                    ws.StreamStatus != StatusId.Inactive)
                .ToListAsync();

            // ── Step 2: no subtasks at all → nothing to evaluate ─────────────────────
            if (!activeSubtasks.Any())
                return false;

            // ── Step 3: check if every subtask is in a "done" status ─────────────────
            // StatusId.IsCompleted checks against CompletedStatuses:
            //   { 6=DevCompleted, 12=FuncFixCompleted, 13=TransportCreated,
            //     14=TransportReleased, 15=MovedToQA, 16=MovedToProduction, 18=Closed }
            bool allDone = activeSubtasks.All(ws =>
                StatusId.IsCompleted(ws.StreamStatus ?? 0));

            if (!allDone)
                return false;

            // ── Step 4: load ticket — need Status, Repo_Id, Issue_Id for update + broadcast
            var ticket = await _db.Set<TicketMaster>()
                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

            if (ticket == null)
                return false;

            // ── Step 5: skip if already closed or cancelled ───────────────────────────
            // Never downgrade or re-trigger completed state
            if (ticket.Status == StatusId.Closed ||
                ticket.Status == StatusId.Cancelled)
                return false;

            // ── Step 6: update ticket status to Closed (18) ──────────────────────────
            // Use UpdateTrackedEntityAsync — respects EF audit (UpdatedAt, UpdatedBy)
            await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                t => t.Issue_Id == issueId,
                t =>
                {
                    t.Status = StatusId.Closed;   // 18 = CLS
                }
            );

            // ── Step 7: get RepoKey for SignalR broadcast ─────────────────────────────
            // Needed to route the broadcast to the correct repo room
            string repoKey = string.Empty;
            try
            {
                if (ticket.RepoId.HasValue)
                    repoKey = await _helperGet.GetRepoKeyByIdAsync(ticket.RepoId);
            }
            catch
            {
                // RepoKey lookup failed — continue without broadcast
                // Ticket is already closed in DB — don't rollback for a SignalR failure
            }

            // ── Step 8: broadcast ticket auto-completion via SignalR ──────────────────
            // Tells all connected clients in this repo that the ticket just completed
            // UI can then update the ticket card status in real time
            if (!string.IsNullOrEmpty(repoKey))
            {
                try
                {
                    // Build a minimal payload — clients only need Status + Issue_Id to update the card
                    var completionPayload = new
                    {
                        Issue_Id = issueId,
                        Status = StatusId.Closed,
                        StatusName = "Closed",
                        ClosedAt = DateTime.UtcNow,
                        AutoClosed = true,          // flag so UI can show "auto-completed" toast
                    };

                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "TicketsList",
                        Action = "StatusUpdate",   // same action as manual status update
                        Payload = completionPayload,
                        KeyField = "Issue_Id",
                        IssueId = issueId,
                        RepoKey = repoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // SignalR failure must never break the response
                    // Ticket is already closed — just log and continue
                    Console.WriteLine(
                        $"[WorkStreamService] SignalR broadcast failed for ticket {issueId}: {ex.Message}");
                }
            }

            return true;  // ticket was just auto-completed
        }
        private async Task<string?> GetStatusNameAsync(int? statusId)
        {
            var status = await _db.StatusMasters
                .Where(s => s.Status_Id == statusId)
                .Select(s => new { s.Status_Name })
                .FirstOrDefaultAsync();

            return status?.Status_Name;
        }

        public async Task<TicketStatusResult> ComputeAndUpdateTicketStatusAsync(Guid issueId)
        {
            // ── Step 1: load all non-inactive subtasks with their status Sort_Order ───
            // Join with Status_Master to get Sort_Order for stage comparison
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

            // ── Step 2: no subtasks → nothing to compute ─────────────────────────────
            if (!subtasks.Any())
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

            // ── Step 3: compute OverallPct ────────────────────────────────────────────
            // Simple average of all non-inactive CompletionPct values
            // Treat null CompletionPct as 0
            var overallPct = Math.Round(
                subtasks.Average(s => (double)(s.CompletionPct ?? 0)),
                2);

            var totalSubtasks = subtasks?.Count;
            var completedSubtasks = subtasks.Count(s => s.IsCompleted);
            var activeSubtasks = subtasks.Count(s => !s.IsCompleted);

            // ── Step 4: determine computed status ────────────────────────────────────
            // Check if ALL are completed first
            bool allCompleted = subtasks.All(s => s.IsCompleted);

            int computedStatusId;
            string computedStatusName;

            if (allCompleted)
            {
                // Every subtask is done → auto-close
                computedStatusId = StatusId.Closed;
                computedStatusName = "Closed";
            }
            else
            {
                // Find the ACTIVE subtask with the HIGHEST Sort_Order
                // "Highest Sort_Order" = most advanced stage in the pipeline
                // This means: if anyone is in Testing (Sort=8) while others are
                // still in Development (Sort=5), the ticket reflects Testing
                var mostAdvancedActive = subtasks
                    .Where(s => !s.IsCompleted)      // only active ones
                    .OrderByDescending(s => s.Sort_Order)
                    .First();

                computedStatusId = mostAdvancedActive.StreamStatus!.Value;
                computedStatusName = mostAdvancedActive.Status_Name;
            }

            // ── Step 5: load current ticket ──────────────────────────────────────────
            var ticket = await _db.Set<TicketMaster>()
                .FirstOrDefaultAsync(t => t.Issue_Id == issueId);

            if (ticket == null)
            {
                return new TicketStatusResult
                {
                    ComputedStatusId = computedStatusId,
                    ComputedStatusName = computedStatusName,
                    OverallPct = (decimal)overallPct,
                    TotalSubtasks = totalSubtasks,
                    CompletedSubtasks = completedSubtasks,
                    ActiveSubtasks = activeSubtasks,
                    TicketAutoCompleted = allCompleted,
                };
            }

            // ── Step 6: skip update if ticket is already terminal ────────────────────
            bool isAlreadyTerminal =
                ticket.Status == StatusId.Closed ||
                ticket.Status == StatusId.Cancelled;

            if (!isAlreadyTerminal)
            {
                // Update Status + OverallPct in one call
                await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                    t => t.Issue_Id == issueId,
                    t =>
                    {
                        t.Status = computedStatusId;
                        t.CompletionPct = (decimal?)overallPct;
                        t.StatusName = computedStatusName;
                        // UpdatedAt, UpdatedBy → DBContext audit
                    }
                );
            }

            // ── Step 7: get RepoKey for broadcast ────────────────────────────────────
            string repoKey = string.Empty;
            try
            {
                if (ticket.RepoId.HasValue)
                    repoKey = await _helperGet.GetRepoKeyByIdAsync(ticket.RepoId.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WorkStreamService] RepoKey lookup failed for {issueId}: {ex.Message}");
            }

            // ── Step 8: broadcast live status update ─────────────────────────────────
            // Fires on EVERY subtask change — not just on completion
            // UI receives this and updates the ticket card status + progress bar in real time
            if (!string.IsNullOrEmpty(repoKey) && !isAlreadyTerminal)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "TicketsList",
                        Action = "StatusUpdate",
                        Payload = new
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
                        },
                        KeyField = "Issue_Id",
                        IssueId = issueId,
                        RepoKey = repoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Never break the response for a SignalR failure
                    Console.WriteLine(
                        $"[WorkStreamService] Broadcast failed for {issueId}: {ex.Message}");
                }
            }

            return new TicketStatusResult
            {
                ComputedStatusId = computedStatusId,
                ComputedStatusName = computedStatusName,
                OverallPct = (decimal)overallPct,
                TotalSubtasks = totalSubtasks,
                CompletedSubtasks = completedSubtasks,
                ActiveSubtasks = activeSubtasks,
                TicketAutoCompleted = allCompleted,
            };
        }
    }
}
