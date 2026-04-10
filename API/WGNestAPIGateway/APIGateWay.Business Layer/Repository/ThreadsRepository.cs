using APIGateWay.BusinessLayer.Auth;
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
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ReverseMarkdown.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Repository
{
    public class ThreadsRepository : IThreadsRepository
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        private readonly ISyncExecutionService _syncExecutionService;
        private readonly APIGatewayDBContext _dBContext;
        private readonly IWorkStreamService _workStreamService;
        public ThreadsRepository(
            IDomainService domainService, APIGateWayCommonService service,
            APIGatewayDBContext dbContext,
            IMapper mapper, ILoginContextService loginContext, IAttachmentService attachmentService,
            IHelperGetData helperGet, IRealtimeNotifier realtimeNotifier, ISyncExecutionService syncExecutionService, IWorkStreamService workStreamService)
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;
            _dBContext = dbContext;
            _workStreamService = workStreamService;
        }
        private static readonly HashSet<string> _selfResourceStreams =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "IN_PROGRESS",
            "HOLD",
            "AWAITING_CLIENT"
        };

        public async Task<ThreadList> CreateThreadAsync(PostThreadsDto threadDto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            ThreadList finalThreadData = null;
            IssueRepositoryInfo issueRepoInfo = null;
          
            WorkStream workStream = null; 
            long newThreadId = 0; // Capture the new ID to filter it later

            try
            {
                finalThreadData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var threadMaster = _mapper.Map<ThreadMaster>(threadDto);

                    issueRepoInfo = await _helperGet.GetIssueRepositoryInfoAsync(threadDto.Issue_Id);

                    if (issueRepoInfo != null)
                    {
                        threadMaster.IssueTitle = issueRepoInfo.IssueTitle;
                    }
                    var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
                    threadMaster.ThreadId = seq.CurrentValue;
                    newThreadId = seq.CurrentValue; // Save the ID for the Sync call later

                    string finalHtmlDescription = threadDto.CommentText;

                    if (threadDto.temp?.temps != null && threadDto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var permFolder = $"{threadMaster.ThreadId}-{threadDto.Issue_Id}";
                        var relativePath = $"{permUserId}/{permFolder}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            threadDto.CommentText, threadDto.temp.temps, relativePath, threadMaster.ThreadId.ToString(), "ThreadMaster"
                        );

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    threadMaster.HtmlDesc = finalHtmlDescription;
                    threadMaster.CommentText = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);
                    // ── WorkStream: single upsert for this assignee ───────────
                    // One thread = one person posting = one ResourceId
                    // StreamName is auto-resolved from EMPLOYEEMASTER.Team of ResourceId
                    // NOT from _loginContext.userId — from the passed ResourceId
                    var resolvedResourceId = threadDto.ResourceId ?? _loginContext.userId;
                    
                        var streamResult = await _workStreamService.UpsertWorkStreamAsync(
                            new WorkStreamContext
                            {
                                IssueId = threadDto.Issue_Id,
                                ResourceId = resolvedResourceId,
                                StreamStatus = threadDto.StreamStatus,
                                CompletionPct = threadDto.CompletionPct,
                                TargetDate = threadDto.TargetDate,
                                ParentThreadId = threadMaster.ThreadId
                            }
                        );
                    await _domainService.SaveEntityWithAttachmentsAsync(threadMaster, attachmentResult?.Attachments);

                    if (threadDto.temp?.temps != null && threadDto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(threadDto.temp);
                    }

                    // Return the basic mapped data to escape the transaction block
                    return _mapper.Map<ThreadList>(threadMaster);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }

                throw new Exception("Ticket creation failed. Everything was rolled back safely.", ex);
            }

             //====================================================================
             //🔥 FETCH RICH DATA VIA SYNC CONFIG(AFTER TRANSACTION COMMITS)
             //====================================================================
            ThreadList freshThreadData = null;
            // 1. Prepare parameters for the Stored Procedure
            var syncParams = new Dictionary<string, string>
                {
                    { "IssuesId", threadDto.Issue_Id.ToString() }
                };

            // 2. Execute directly using ExecuteLocalAsync<T>
            var syncResponse = await _syncExecutionService.ExecuteLocalAsync<ThreadList>(
                databaseName: "", // Your method uses _loginContext.databaseName internally if this is null/empty
                storedProcedure: "GETTHREADLIST",
                lastSync: null,
                parameters: syncParams,
                source: "CreateThreadService"
            );

            // 3. Extract the exact thread we just created
            if (syncResponse.Ok && syncResponse.Data != null)
            {
                // Try to cast directly first (this is what ExecuteGetItemAsyc<T> usually returns)
                var threads = syncResponse.Data as IEnumerable<ThreadList>;

                // Fallback: If your data layer returns a JsonElement instead of a typed list
                if (threads == null && syncResponse.Data is System.Text.Json.JsonElement jsonElement)
                {
                    threads = System.Text.Json.JsonSerializer.Deserialize<List<ThreadList>>(jsonElement.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                // Find the rich thread data for the one we just inserted
                var richThreadData = threads?.FirstOrDefault(t => t.ThreadId == newThreadId);
                if (richThreadData != null)
                {
                    freshThreadData = richThreadData; // Overwrite the basic mapped data with the rich SP data
                }
            }

            //var freshThreadData = new ThreadList
            //{
            //    ThreadId = 9999,
            //    CommentText = "This is a dummy thread comment for SignalR testing.",
            //    HtmlDesc = "<p>This is a <b>dummy</b> thread comment for SignalR testing.</p>",
            //    Issue_Id = threadDto.Issue_Id, // Use incoming Issue_Id
            //    CreatedBy = "TestUser",
            //    CreatedAt = DateTime.UtcNow,
            //    UpdatedBy = null,
            //    UpdatedAt = null,
            //    From_Time = DateTime.UtcNow,
            //    To_Time = DateTime.UtcNow.AddHours(1),
            //    Hours = "1"
            //};

            //// 🔥 Fake repo info for RepoKey (since you're skipping DB)
            //var issueRepoInfo = new IssueRepositoryInfo
            //{
            //    RepoKey = "R80.21wd",  // Put any test repo key
            //    IssueTitle = "Dummy Issue"
            //};
            // ====================================================================
            // 🔥 BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
            // ====================================================================
            if (freshThreadData != null && issueRepoInfo != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "ThreadsList",
                        Action = "Create",
                        Payload = freshThreadData, // Now contains the rich data from GETTHREADLIST
                        KeyField = "ThreadId",
                        IssueId = threadDto.Issue_Id,
                        RepoKey = issueRepoInfo.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast Ticket creation: {ex.Message}");
                }
            }

            // 6. Return to the Controller
            return freshThreadData;
        }

        public async Task<ThreadList> UpdateThreadAsync(long threadId, UpdateThreadDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            ThreadList finalThreadData = null;
            IssueRepositoryInfo issueRepoInfo = null;

            try
            {
                finalThreadData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // 1. Fetch existing Thread
                    var existingThread = await _dBContext.ISSUETHREADS.FindAsync(threadId);
                    if (existingThread == null)
                        throw new Exception("Thread not found");

                    // ====================================================================
                    // 2. 🔥 FETCH THE EXACT BASE WORKSTREAM (using WorkStreamId or ResourceId)
                    // ====================================================================
                    WorkStream existingWorkStream = null;

                    if (dto.WorkStreamId.HasValue && dto.WorkStreamId.Value != Guid.Empty)
                    {
                        // Strict match if WorkStreamId is provided
                        existingWorkStream = await _dBContext.WorkStreams
                            .FirstOrDefaultAsync(ws => ws.StreamId == dto.WorkStreamId.Value);
                    }
                    else if (dto.ResourceId.HasValue)
                    {
                        // Fallback to ResourceId if WorkStreamId is missing
                        existingWorkStream = await _dBContext.WorkStreams
                            .FirstOrDefaultAsync(ws => ws.ParentThreadId == threadId && ws.ResourceId == dto.ResourceId.Value);

                        if (existingWorkStream == null)
                        {
                            existingWorkStream = await _dBContext.WorkStreams
                                .FirstOrDefaultAsync(ws => ws.IssueId == existingThread.Issue_Id &&
                                                           ws.ResourceId == dto.ResourceId.Value &&
                                                           ws.StreamStatus != StatusId.Inactive);
                        }
                    }

                    // ====================================================================
                    // 3. 🔥 UPDATE THE BASE WORKSTREAM (Status & Completion Pct)
                    // ====================================================================
                    if (existingWorkStream != null)
                    {
                        bool wsChanged = false;

                        // Update Status ONLY if it's a new value
                        if (dto.StreamStatus.HasValue && dto.StreamStatus.Value != existingWorkStream.StreamStatus)
                        {
                            existingWorkStream.StreamStatus = dto.StreamStatus.Value;
                            wsChanged = true;
                        }

                        // Update Completion Pct
                        if (dto.CompletionPct.HasValue && dto.CompletionPct.Value != existingWorkStream.CompletionPct)
                        {
                            existingWorkStream.CompletionPct = dto.CompletionPct.Value;
                            wsChanged = true;
                        }

                        if (wsChanged)
                        {
                            _dBContext.WorkStreams.Update(existingWorkStream);
                            await _dBContext.SaveChangesAsync(); // Commit the WorkStream changes
                        }
                    }

                    // 4. Process Attachments
                    string finalHtmlDescription = dto.CommentText ?? existingThread.HtmlDesc;
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var permFolder = $"{threadId}-{existingThread.Issue_Id}";
                        var relativePath = $"{permUserId}/{permFolder}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            dto.CommentText, dto.temp.temps, relativePath, threadId.ToString(), "ThreadMaster"
                        );
                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    // 5. Update ThreadMaster Entity
                    var updatedThread = await _domainService.UpdateEntityWithAttachmentsAsync<ThreadMaster>(
                        threadId,
                        entity =>
                        {
                            if (!string.IsNullOrEmpty(dto.CommentText))
                            {
                                entity.HtmlDesc = finalHtmlDescription;
                                entity.CommentText = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);
                            }

                            if (dto.From_Time.HasValue) entity.From_Time = dto.From_Time.Value;
                            if (dto.To_Time.HasValue) entity.To_Time = dto.To_Time.Value;
                            if (dto.CompletionPct.HasValue) entity.CompletionPct = dto.CompletionPct.Value;
                            if (!string.IsNullOrEmpty(dto.Hours)) entity.Hours = dto.Hours;

                            // 🔥 "Update this completion pct to thread"
                            // NOTE: Make sure `CompletionPct` actually exists in your ThreadMaster entity!
                            // if (dto.CompletionPct.HasValue) entity.CompletionPct = dto.CompletionPct.Value; 
                        },
                        attachmentResult?.Attachments
                    );

                    // ====================================================================
                    // 6. 🔥 MULTIPLE ASSIGNEES / HANDOFF SYNC LOGIC
                    // ====================================================================
                    //if (existingWorkStream != null && dto.NextAssignees != null)
                    //{
                    //    // 1. 🔥 DEDUPLICATE THE INCOMING UI LIST 
                    //    // (This prevents the bug where the UI sends multiple identical assignees)
                    //    var distinctIncomingAssignees = dto.NextAssignees
                    //        .GroupBy(a => a.Id)
                    //        .Select(g => g.First())
                    //        .ToList();

                    //    var incomingResourceIds = distinctIncomingAssignees.Select(na => na.Id).ToList();

                    //    // 2. Find existing handoffs for THIS Thread and THIS Issue
                    //    var existingHandoffs = await _dBContext.Set<WorkStreamHandoff>()
                    //        .Where(h => h.InitiatingThreadId == threadId &&
                    //                    h.IssueId == existingThread.Issue_Id &&
                    //                    h.Status != HandoffStatus.Inactive)
                    //        .ToListAsync();

                    //    // 3. Map Handoffs to their Target ResourceIds
                    //    var existingTargetStreamIds = existingHandoffs.Select(h => h.TargetStreamId).ToList();
                    //    var existingTargetStreams = await _dBContext.WorkStreams
                    //        .Where(ws => existingTargetStreamIds.Contains(ws.StreamId))
                    //        .ToListAsync();

                    //    var existingHandoffAssignees = existingHandoffs
                    //        .Select(h => new
                    //        {
                    //            Handoff = h,
                    //            ResourceId = existingTargetStreams.FirstOrDefault(ws => ws.StreamId == h.TargetStreamId)?.ResourceId ?? Guid.Empty
                    //        })
                    //        .Where(x => x.ResourceId != Guid.Empty)
                    //        .ToList();

                    //    var existingResourceIds = existingHandoffAssignees.Select(e => e.ResourceId).ToList();

                    //    // 🔴 REMOVE MISSING: In DB but removed from UI -> Mark Inactive
                    //    foreach (var existing in existingHandoffAssignees)
                    //    {
                    //        if (!incomingResourceIds.Contains(existing.ResourceId))
                    //        {
                    //            existing.Handoff.Status = HandoffStatus.Inactive;
                    //            existing.Handoff.UpdatedAt = DateTime.UtcNow;
                    //            _dBContext.Set<WorkStreamHandoff>().Update(existing.Handoff);
                    //        }
                    //    }

                    //    // 🟢 ADD NEW: In UI but not in DB -> Create WorkStream & Handoff
                    //    foreach (var incomingAssignee in distinctIncomingAssignees)
                    //    {
                    //        if (!existingResourceIds.Contains(incomingAssignee.Id))
                    //        {
                    //            // 1. Ensure they have an active WorkStream across the issue
                    //            // NOTE: We DO NOT pass dto.CompletionPct here, because that belongs to the thread editor!
                    //            // Passing null ensures the target assignee keeps their existing percentage.
                    //            var targetStreamResult = await _workStreamService.UpsertWorkStreamAsync(new WorkStreamContext
                    //            {
                    //                IssueId = existingThread.Issue_Id,
                    //                ResourceId = incomingAssignee.Id,
                    //                StreamStatus = incomingAssignee.StreamId > 0 ? incomingAssignee.StreamId : null,
                    //                ParentThreadId = threadId
                    //            });

                    //            // 2. Create the Handoff link
                    //            var seq = await _commonService.GetNextSequenceAsync("WorkStreamsHandsoff");
                    //            var newHandoff = new WorkStreamHandoff
                    //            {
                    //                HandsOffId = seq.CurrentValue,
                    //                IssueId = existingThread.Issue_Id,
                    //                SourceStreamId = existingWorkStream.StreamId,
                    //                TargetStreamId = targetStreamResult.StreamId,
                    //                InitiatingThreadId = threadId,
                    //                Status = HandoffStatus.Pending
                    //            };

                    //            _dBContext.Set<WorkStreamHandoff>().Add(newHandoff);
                    //        }
                    //        // If they already exist in existingResourceIds, do nothing!
                    //    }

                    //    await _dBContext.SaveChangesAsync(); // Commit all handoff changes
                    //}
                    // 7. Cleanup temporary files
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(dto.temp);
                    }

                    return _mapper.Map<ThreadList>(updatedThread);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }
                throw new Exception($"Thread update failed. Everything was rolled back safely. Error: {ex.Message}", ex);
            }

            // ====================================================================
            // 🔥 FETCH RICH DATA VIA SYNC CONFIG (AFTER TRANSACTION COMMITS)
            // ====================================================================
            issueRepoInfo = await _helperGet.GetIssueRepositoryInfoAsync(finalThreadData.Issue_Id);
            ThreadList freshThreadData = null;

            var syncParams = new Dictionary<string, string>
            {
                { "IssuesId", finalThreadData.Issue_Id.ToString() }
            };

            var syncResponse = await _syncExecutionService.ExecuteLocalAsync<ThreadList>(
                databaseName: "",
                storedProcedure: "GETTHREADLIST",
                lastSync: null,
                parameters: syncParams,
                source: "UpdateThreadService"
            );

            if (syncResponse.Ok && syncResponse.Data != null)
            {
                var threads = syncResponse.Data as IEnumerable<ThreadList>;

                if (threads == null && syncResponse.Data is System.Text.Json.JsonElement jsonElement)
                {
                    threads = System.Text.Json.JsonSerializer.Deserialize<List<ThreadList>>(
                        jsonElement.GetRawText(),
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }

                freshThreadData = threads?.FirstOrDefault(t => t.ThreadId == threadId);
            }

            // ====================================================================
            // 🔥 BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
            // ====================================================================
            if (freshThreadData != null && issueRepoInfo != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "ThreadsList",
                        Action = "Update",
                        Payload = freshThreadData,
                        KeyField = "ThreadId",
                        IssueId = finalThreadData.Issue_Id,
                        RepoKey = issueRepoInfo.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast Thread update: {ex.Message}");
                }
            }

            return freshThreadData ?? finalThreadData;
        }

    }
}
