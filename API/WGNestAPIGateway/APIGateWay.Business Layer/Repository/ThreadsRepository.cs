using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.Hub;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
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
        public ThreadsRepository(
            IDomainService domainService, APIGateWayCommonService service,
            IMapper mapper, ILoginContextService loginContext, IAttachmentService attachmentService,
            IHelperGetData helperGet, IRealtimeNotifier realtimeNotifier)
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
        }
        public async Task<ThreadList> CreateThreadAsync(PostThreadsDto threadDto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            ThreadList finalThreadData = null; // 🔥 3. Create a variable to hold the result
            IssueRepositoryInfo issueRepoInfo = null;

            try
            {
                // 🔥 4. Assign the result of the transaction to your variable
                finalThreadData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var threadMaster = _mapper.Map<ThreadMaster>(threadDto);

                    //if (!threadDto.Issue_Id.HasValue)
                    //{
                    //    throw new Exception("Repo_Id is required to create a Ticket.");
                    //}
                    issueRepoInfo = await _helperGet.GetIssueRepositoryInfoAsync(threadDto.Issue_Id);

                    if (issueRepoInfo != null) // Ensure issueRepoInfo is not null
                    {
                        threadMaster.IssueTitle = issueRepoInfo.IssueTitle;
                    }
                    var seq = await _commonService.GetNextSequenceAsync("ISSUETHREADS");
                    threadMaster.ThreadId = seq.CurrentValue;

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

                    await _domainService.SaveEntityWithAttachmentsAsync(threadMaster, attachmentResult?.Attachments);

                    if (threadDto.temp?.temps != null && threadDto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(threadDto.temp);
                    }

                    // Return the mapped data OUT of the transaction block
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

            // ====================================================================
            // 🔥 5. BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
            // ====================================================================
            if (finalThreadData != null && issueRepoInfo != null)
            {
                // We use a try-catch here so that if the SignalR server is offline, 
                // it doesn't crash the API and return a 500 error to the user who just successfully created a project!
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "ThreadsList",
                        Action = "Create",
                        Payload = finalThreadData,
                        KeyField = "Issue_Id",
                        RepoKey = issueRepoInfo.RepoKey,  // Using the already fetched issueRepoInfo
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Log the broadcasting error, but let the method finish successfully
                    Console.WriteLine($"Failed to broadcast Ticket creation: {ex.Message}");
                }
            }

            // 6. Return to the Controller
            return finalThreadData;
        }
    }
}
