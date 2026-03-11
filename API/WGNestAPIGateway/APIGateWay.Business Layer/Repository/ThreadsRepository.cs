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
                                StreamStatus = threadDto.StreamStatus ?? WorkStreamStatus.InProgress,
                                CompletionPct = threadDto.CompletionPct,
                                TargetDate = threadDto.TargetDate,
                                // StreamName → auto-resolved inside service from assignee's dept
                            }
                        );

                        // Update TicketMaster to always point to the latest active stream
                        // This lets the ticket card show "who is currently working on it"
                        //await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                        //    ticket => ticket.Issue_Id == threadDto.Issue_Id,
                        //    ticket =>
                        //    {
                        //        ticket.StreamId = streamResult.StreamId;
                        //        ticket.StreamName = streamResult.StreamName;  // dept name
                        //        ticket.ResourceId = streamResult.ResourceId;
                        //    }
                        //);
                   


                    //// ====================================================================
                    //// 🔥 1. DETERMINE DYNAMIC RESOURCE ID
                    //// ====================================================================
                    //var resolvedResourceId = _selfResourceStreams.Contains(threadDto.StreamName ?? "")
                    // ? _loginContext.userId
                    // : threadDto.ResourceId;


                    //// ====================================================================
                    //// 🔥 2. HANDOFF LOGIC: Close previous stream if changing phases
                    //// ====================================================================
                    //var previousActiveStream = await _dBContext.WorkStreams
                    //    .Where(ws => ws.IssueId == threadDto.Issue_Id
                    //              && ws.ResourceId == _loginContext.userId // Only target THIS user's active work
                    //              && ws.StreamStatus == 1                  // Assuming 1 = Active/In Progress
                    //              && ws.StreamName != threadDto.StreamName) // Ensure they are actually changing the phase
                    //    .FirstOrDefaultAsync();

                    //if (previousActiveStream != null)
                    //{
                    //    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    //        ws => ws.Id == previousActiveStream.Id,
                    //        ws =>
                    //        {
                    //            ws.StreamStatus = 2;       // Assuming 2 = Completed / Closed
                    //            ws.CompletionPct = 100;    // Auto-complete their portion of the work
                    //        }
                    //    );
                    //}

                    //// ====================================================================
                    //// 🔥 3. UPSERT LOGIC: Handle the new/current stream
                    //// ====================================================================
                    //// Check LAST row for this IssueId
                    //var lastWorkStream = await _dBContext.WorkStreams
                    //    .Where(ws => ws.IssueId == threadDto.Issue_Id)
                    //    .OrderByDescending(ws => ws.CreatedAt)
                    //    .FirstOrDefaultAsync();

                    //// Match only if the last row has the SAME StreamName as current post
                    //var existingWorkStream = (lastWorkStream?.StreamName == threadDto.StreamName)
                    //    ? lastWorkStream
                    //    : null;

                    //if (existingWorkStream != null)
                    //{
                    //    // Last row is same StreamName → UPDATE CompletionPct only
                    //    await _domainService.UpdateTrackedEntityAsync<WorkStream>(
                    //        ws => ws.Id == existingWorkStream.Id,   // use Id — safest predicate
                    //        ws =>
                    //        {
                    //            ws.CompletionPct = threadDto.CompletionPct ?? ws.CompletionPct;
                    //            ws.ResourceId = resolvedResourceId;
                    //        }
                    //    );
                    //    workStream = existingWorkStream;
                    //}
                    //else
                    //{
                    //    // Last row is different StreamName (or no rows yet) → INSERT new row
                    //    workStream = new WorkStream
                    //    {
                    //        IssueId = threadDto.Issue_Id,
                    //        StreamName = threadDto.StreamName,
                    //        ResourceId = resolvedResourceId,
                    //        StreamStatus = 1,
                    //        CompletionPct = threadDto.CompletionPct ?? 0,
                    //        TargetDate = threadDto.TargetDate,
                    //    };
                    //    await _domainService.SaveEntityWithAttachmentsAsync(workStream, null);
                    //}
                    // UPDATE TicketMaster — always runs, insert or update path
                    //await _domainService.UpdateTrackedEntityAsync<TicketMaster>(
                    //    ticket => ticket.Issue_Id == threadDto.Issue_Id,
                    //    ticket =>
                    //    {
                    //        ticket.StreamId = workStream.StreamId;
                    //        ticket.StreamName = workStream.StreamName;
                    //        ticket.ResourceId = workStream.ResourceId;
                    //    }
                    //);
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
        //public async Task<ThreadList> CreateThreadAsync(PostThreadsDto threadDto)
        //{
        //    ProcessedAttachmentResult attachmentResult = null;
        //    ThreadList finalThreadData = null; // 🔥 3. Create a variable to hold the result
        //    IssueRepositoryInfo issueRepoInfo = null;

        //    try
        //    {
        //        // 🔥 4. Assign the result of the transaction to your variable
        //        finalThreadData = await _domainService.ExecuteInTransactionAsync(async () =>
        //        {
        //            var threadMaster = _mapper.Map<ThreadMaster>(threadDto);

        //            //if (!threadDto.Issue_Id.HasValue)
        //            //{
        //            //    throw new Exception("Repo_Id is required to create a Ticket.");
        //            //}
        //            issueRepoInfo = await _helperGet.GetIssueRepositoryInfoAsync(threadDto.Issue_Id);

        //            if (issueRepoInfo != null) // Ensure issueRepoInfo is not null
        //            {
        //                threadMaster.IssueTitle = issueRepoInfo.IssueTitle;
        //            }
        //            var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
        //            threadMaster.ThreadId = seq.CurrentValue;

        //            string finalHtmlDescription = threadDto.CommentText;

        //            if (threadDto.temp?.temps != null && threadDto.temp.temps.Any())
        //            {
        //                var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
        //                var permFolder = $"{threadMaster.ThreadId}-{threadDto.Issue_Id}";
        //                var relativePath = $"{permUserId}/{permFolder}";

        //                attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
        //                    threadDto.CommentText, threadDto.temp.temps, relativePath, threadMaster.ThreadId.ToString(), "ThreadMaster"
        //                );

        //                finalHtmlDescription = attachmentResult.UpdatedHtml;
        //            }

        //            threadMaster.HtmlDesc = finalHtmlDescription;
        //            threadMaster.CommentText = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

        //            await _domainService.SaveEntityWithAttachmentsAsync(threadMaster, attachmentResult?.Attachments);

        //            if (threadDto.temp?.temps != null && threadDto.temp.temps.Any())
        //            {
        //                await _attachmentService.CleanupTempFiles(threadDto.temp);
        //            }
        //            var thread =  _syncExecutionService.ExecuteLocalAsync("thread")
        //            // Return the mapped data OUT of the transaction block
        //            return _mapper.Map<ThreadList>(threadMaster);
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
        //        {
        //            _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
        //        }

        //        throw new Exception("Ticket creation failed. Everything was rolled back safely.", ex);
        //    }
        //    //var finalThreadData = new ThreadList
        //    //{
        //    //    ThreadId = 9999,
        //    //    CommentText = "This is a dummy thread comment for SignalR testing.",
        //    //    HtmlDesc = "<p>This is a <b>dummy</b> thread comment for SignalR testing.</p>",
        //    //    Issue_Id = threadDto.Issue_Id, // Use incoming Issue_Id
        //    //    CreatedBy = "TestUser",
        //    //    CreatedAt = DateTime.UtcNow,
        //    //    UpdatedBy = null,
        //    //    UpdatedAt = null,
        //    //    From_Time = DateTime.UtcNow,
        //    //    To_Time = DateTime.UtcNow.AddHours(1),
        //    //    Hours = "1"
        //    //};

        //    //// 🔥 Fake repo info for RepoKey (since you're skipping DB)
        //    //var issueRepoInfo = new IssueRepositoryInfo
        //    //{
        //    //    RepoKey = "R80.21wd",  // Put any test repo key
        //    //    IssueTitle = "Dummy Issue"
        //    //};


        //    // ====================================================================
        //    // 🔥 5. BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
        //    // ====================================================================
        //    if (finalThreadData != null && issueRepoInfo != null)
        //    {
        //        // We use a try-catch here so that if the SignalR server is offline, 
        //        // it doesn't crash the API and return a 500 error to the user who just successfully created a project!
        //        try
        //        {
        //            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
        //            {
        //                Entity = "ThreadsList",
        //                Action = "Create",
        //                Payload = finalThreadData,
        //                KeyField = "ThreadId",
        //                IssueId = threadDto.Issue_Id,
        //                RepoKey = issueRepoInfo.RepoKey,  // Using the already fetched issueRepoInfo
        //                Timestamp = DateTime.UtcNow
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            // Log the broadcasting error, but let the method finish successfully
        //            Console.WriteLine($"Failed to broadcast Ticket creation: {ex.Message}");
        //        }
        //    }

        //    // 6. Return to the Controller
        //    return finalThreadData;
        //}
    }
}
