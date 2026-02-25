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
    public class ProjectRepo : IProjectRepo
    {
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        private readonly IHelperGetData _helperGet;
        private readonly IRealtimeNotifier _realtimeNotifier;
        public ProjectRepo(
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

        //public async Task<GetProject> CreateProjectAsync(ProjectDto projectDto)
        //{
        //    ProcessedAttachmentResult attachmentResult = null;
        //    GetProject finalProjectData = null;
        //    finalProjectData = new GetProject
        //    {
        //        Id = Guid.NewGuid(),
        //        Project_Name = "Test Project Alpha",
        //        Description = "This is a clean, plain text description of the project stripped of HTML.",
        //        ProjectKey = "P105",
        //        Repo_Id = Guid.NewGuid(),
        //        RepoKey = "R72",
        //        Repo_Name = "Test Repo2",
        //        Status = "Active",
        //        EmployeeName = "John Doe",
        //        DueDate = DateTime.UtcNow.AddDays(14),
        //        CreatedAt = DateTime.UtcNow,
        //        CreatedBy = Guid.NewGuid().ToString(),
        //        UpdatedAt = null,
        //        UpdatedBy = null
        //    };
        //    // ====================================================================
        //    // 🔥 5. BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
        //    // ====================================================================
        //    if (finalProjectData != null)
        //    {
        //        // We use a try-catch here so that if the SignalR server is offline, 
        //        // it doesn't crash the API and return a 500 error to the user who just successfully created a project!
        //        try
        //        {
        //            await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
        //            {
        //                Entity = "Project",
        //                Action = "Create",
        //                Payload = finalProjectData,
        //                KeyField = "Id",
        //                RepoKey = finalProjectData.RepoKey, // Or finalProjectData.RepoKey if your mapper populated it
        //                Timestamp = DateTime.UtcNow
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            // Log the broadcasting error, but let the method finish successfully
        //            Console.WriteLine($"Failed to broadcast project creation: {ex.Message}");
        //        }
        //    }

        //    // 6. Return to the Controller
        //    return finalProjectData;
        //}

        public async Task<GetProject> CreateProjectAsync(ProjectDto projectDto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            GetProject finalProjectData = null; // 🔥 3. Create a variable to hold the result

            try
            {
                // 🔥 4. Assign the result of the transaction to your variable
                finalProjectData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var projectMaster = _mapper.Map<ProjectMaster>(projectDto);
                    projectMaster.Id = Guid.NewGuid();
                    projectMaster.Status = 1;

                    if (!projectDto.Repo_Id.HasValue)
                    {
                        throw new Exception("Repo_Id is required to create a project.");
                    }
                    string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(projectDto.Repo_Id.Value);

                    var seq = await _commonService.GetNextSequenceAsync(secureRepoKey, "Project", "Proj_Sequence");
                    projectMaster.SiNo = seq.CurrentValue;
                    projectMaster.ProjectKey = $"P{seq.ColumnValue}";

                    string finalHtmlDescription = projectDto.Description;

                    if (projectDto.temp?.temps != null && projectDto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var permFolder = $"{projectMaster.ProjectKey}-{projectDto.Title}";
                        var relativePath = $"{permUserId}/{permFolder}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            projectDto.Description, projectDto.temp.temps, relativePath, projectMaster.Id.ToString(), "ProjectMaster"
                        );

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    projectMaster.HtmlDesc = finalHtmlDescription;
                    projectMaster.Description = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

                    await _domainService.SaveEntityWithAttachmentsAsync(projectMaster, attachmentResult?.Attachments);

                    if (projectDto.temp?.temps != null && projectDto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(projectDto.temp);
                    }

                    // Return the mapped data OUT of the transaction block
                    return _mapper.Map<GetProject>(projectMaster);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }

                throw new Exception("Project creation failed. Everything was rolled back safely.", ex);
            }

            // ====================================================================
            // 🔥 5. BROADCAST SAFELY AFTER THE TRANSACTION IS 100% COMMITTED
            // ====================================================================
            if (finalProjectData != null)
            {
                // We use a try-catch here so that if the SignalR server is offline, 
                // it doesn't crash the API and return a 500 error to the user who just successfully created a project!
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "ProjectList",
                        Action = "Create",
                        Payload = finalProjectData,
                        KeyField = "Id",
                        RepoKey = finalProjectData.RepoKey, // Or finalProjectData.RepoKey if your mapper populated it
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    // Log the broadcasting error, but let the method finish successfully
                    Console.WriteLine($"Failed to broadcast project creation: {ex.Message}");
                }
            }

            // 6. Return to the Controller
            return finalProjectData;
        }
    }   
}
