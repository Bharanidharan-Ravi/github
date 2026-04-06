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
                    // 1. Fetch existing Thread and Workstream
                    var existingThread = await _dBContext.ISSUETHREADS.FindAsync(threadId); // Adjust DbSet name if different
                    if (existingThread == null)
                        throw new Exception("Thread not found");

                    var existingWorkStream = await _dBContext.WorkStreams
                        .FirstOrDefaultAsync(ws => ws.ParentThreadId == threadId);

                    // ====================================================================
                    // 🔥 BUSINESS RULE 1: Time Editing Validation (Today or Yesterday ONLY)
                    // ====================================================================
                    bool isTimeUpdateRequested = dto.From_Time.HasValue || dto.To_Time.HasValue || !string.IsNullOrEmpty(dto.Hours);
                    if (isTimeUpdateRequested)
                    {
                        // Calculate difference in days (ignoring time of day)
                        var daysOld = (DateTime.UtcNow.Date - existingThread.CreatedAt.Value).TotalDays;

                        if (daysOld > 1)
                        {
                            throw new InvalidOperationException("Cannot change the Assignee because this thread's work is already marked as 100% complete.");
                        }
                    }

                    // ====================================================================
                    // 🔥 BUSINESS RULE 2: Assignee Lock if 100% Completed
                    // ====================================================================
                    bool isAssigneeChangeRequested = dto.ResourceId.HasValue &&
                                                     existingWorkStream != null &&
                                                     dto.ResourceId.Value != existingWorkStream.ResourceId;

                    if (isAssigneeChangeRequested)
                    {
                        if (existingWorkStream.CompletionPct == 100)
                        {
                            throw new InvalidOperationException("Cannot change the Assignee because this thread's work is already marked as 100% complete.");
                        }
                    }

                    // 2. Process Attachments
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

                    // 3. Update ThreadMaster Entity
                    var updatedThread = await _domainService.UpdateEntityWithAttachmentsAsync<ThreadMaster>(
                        threadId,
                        entity =>
                        {
                            // Update Description
                            if (!string.IsNullOrEmpty(dto.CommentText))
                            {
                                entity.HtmlDesc = finalHtmlDescription;
                                entity.CommentText = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);
                            }

                            // Update Times (only if validation passed above)
                            if (isTimeUpdateRequested)
                            {
                                if (dto.From_Time.HasValue) entity.From_Time = dto.From_Time.Value;
                                if (dto.To_Time.HasValue) entity.To_Time = dto.To_Time.Value;
                                if (!string.IsNullOrEmpty(dto.Hours)) entity.Hours = dto.Hours;
                            }
                        },
                        attachmentResult?.Attachments
                    );

                    // 4. Update WorkStream (Assignee and Percentage)
                    if (existingWorkStream != null && (isAssigneeChangeRequested || dto.CompletionPct.HasValue || dto.StreamStatus.HasValue))
                    {
                        // If Assignee is changing, mark the old assignee as Inactive for this thread
                        if (isAssigneeChangeRequested)
                        {
                            await _workStreamService.UpsertWorkStreamAsync(new WorkStreamContext
                            {
                                IssueId = existingThread.Issue_Id,
                                ResourceId = existingWorkStream.ResourceId,
                                StreamStatus = StatusId.Inactive, // 🔥 Force old assignee to Inactive
                                ParentThreadId = threadId
                            });
                        }

                        // Upsert the new (or existing) assignee with updated percentage/status
                        var targetResourceId = isAssigneeChangeRequested ? dto.ResourceId.Value : existingWorkStream.ResourceId;

                        await _workStreamService.UpsertWorkStreamAsync(new WorkStreamContext
                        {
                            IssueId = existingThread.Issue_Id,
                            ResourceId = targetResourceId,
                            StreamStatus = dto.StreamStatus ?? existingWorkStream.StreamStatus,
                            CompletionPct = dto.CompletionPct ?? existingWorkStream.CompletionPct,
                            TargetDate = dto.TargetDate ?? existingWorkStream.TargetDate,
                            ParentThreadId = threadId
                        });
                    }

                    // 5. Cleanup temporary files
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(dto.temp);
                    }

                    // Return basic mapped data to escape the transaction block
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
