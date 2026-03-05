using APIGateWay.Business_Layer.Helper;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.SignalRHub;
using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
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
        private readonly ISyncExecutionService _syncExecutionService;
        public ProjectRepo(
            IDomainService domainService, APIGateWayCommonService service,
            IMapper mapper, ILoginContextService loginContext, IAttachmentService attachmentService, 
            IHelperGetData helperGet, IRealtimeNotifier realtimeNotifier, ISyncExecutionService syncExecutionService)
        {
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
            _helperGet = helperGet;
            _realtimeNotifier = realtimeNotifier;
            _syncExecutionService = syncExecutionService;
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
                    projectMaster.Status = 1;

                    if (!projectDto.Repo_Id.HasValue)
                    {
                        throw new Exception("Repo_Id is required to create a project.");
                    }
                    string secureRepoKey = await _helperGet.GetRepoKeyByIdAsync(projectDto.Repo_Id.Value);

                    var seq = await _commonService.GetNextSequenceAsync(secureRepoKey, "Project", "Proj_Sequence");
                    projectMaster.SiNo = seq.CurrentValue;
                    projectMaster.ProjectKey = $"P{seq.ColumnValue}";
                    projectMaster.RepoKey = secureRepoKey;
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
                        Entity = "Project",
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

        // ─────────────────────────────────────────────────────────────────────
        // FULL UPDATE
        // PUT /api/project/{id}
        //
        // What changes:  Title, HtmlDesc, Description, DueDate, Status (optional)
        //                New attachments added if uploaded
        // What NEVER changes: Id, ProjectKey, SiNo, Repo_Id, CreatedAt, CreatedBy
        // Auto-updated:  UpdatedAt, UpdatedBy — DBContext audit handles this
        //
        // No sequence call — ProjectKey never regenerates on update.
        // Repo scope validated by RepoScopeHandler BEFORE this runs.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetProject> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            ProcessedAttachmentResult attachmentResult = null;
            GetProject finalProjectData = null;

            try
            {
                finalProjectData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    string finalHtmlDescription = dto.Description ?? string.Empty;

                    // Process new attachments if uploaded
                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                    {
                        // Folder uses userId + projectId — consistent on update
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var relativePath = $"{permUserId}/{projectId}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            dto.Description, dto.temp.temps, relativePath,
                            projectId.ToString(), "ProjectMaster");

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    // Capture for use inside lambda (closure)
                    var capturedHtml = finalHtmlDescription;

                    // UpdateEntityWithAttachmentsAsync:
                    //   → finds ProjectMaster by projectId (EF tracks it)
                    //   → calls your lambda — ONLY listed fields are marked Modified
                    //   → DBContext audit sets UpdatedAt/UpdatedBy on SaveChangesAsync
                    //   → CreatedAt/CreatedBy protected automatically
                    var updatedProject = await _domainService.UpdateEntityWithAttachmentsAsync<ProjectMaster>(
                        projectId,
                        entity =>
                        {
                            entity.Title = dto.Title;
                            entity.HtmlDesc = capturedHtml;
                            entity.Description = HtmlUtilities.ConvertToPlainText(capturedHtml);

                            if (dto.DueDate.HasValue)
                                entity.DueDate = dto.DueDate.Value;

                            // Status is optional in full update — only change if sent
                            if (dto.Status.HasValue)
                                entity.Status = dto.Status.Value;

                            // Id, ProjectKey, SiNo, Repo_Id, CreatedAt, CreatedBy
                            // are NOT listed here = EF never sends them to DB
                        },
                        attachmentResult?.Attachments
                    );

                    if (dto.temp?.temps != null && dto.temp.temps.Any())
                        await _attachmentService.CleanupTempFiles(dto.temp);

                    return _mapper.Map<GetProject>(updatedProject);
                });
            }
            catch (Exception ex)
            {
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);

                throw new Exception("Project update failed. Everything was rolled back safely.", ex);
            }
            var richProjectData = await _syncExecutionService.FetchRichDataAsync<GetProject>(

                configKey: "ProjectList",
                syncParams: new Dictionary<string, string> { { "ProjId", finalProjectData.Id.ToString() } },
                matchPredicate: p => p.Id == finalProjectData.Id,
                fallbackData: finalProjectData,
                lastSync: null // Optional: pass DateTimeOffset if your SP requires it
            );

            if (richProjectData != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "Project",
                        Action = "Update",
                        Payload = richProjectData,
                        KeyField = "Id",
                        RepoKey = richProjectData.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast project update: {ex.Message}");
                }
            }

            return richProjectData;
        }

        // ─────────────────────────────────────────────────────────────────────
        // STATUS-ONLY UPDATE
        // PATCH /api/project/{id}/status
        // Body: { "Status": 2 }
        //
        // Only Status column changes in DB.
        // EF sends: UPDATE ProjectMasters SET Status = @p0, UpdatedAt = @p1, UpdatedBy = @p2
        //           WHERE Id = @p3
        // Everything else in the row stays exactly as-is.
        //
        // No Repo_Id in body — RepoScopeHandler looked up this project's Repo_Id
        // from DB using {id} route param and validated before reaching here.
        // ─────────────────────────────────────────────────────────────────────
        public async Task<GetProject> UpdateProjectStatusAsync(Guid projectId, UpdateStatusDto dto)
        {
            GetProject finalProjectData = null;

            try
            {
                finalProjectData = await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    var updatedProject = await _domainService.UpdateEntityWithAttachmentsAsync<ProjectMaster>(
                        projectId,
                        entity =>
                        {
                            // Only Status — nothing else touches the row
                            entity.Status = dto.Status;
                        }
                        // no newAttachments = null by default
                    );

                    return _mapper.Map<GetProject>(updatedProject);
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Project status update failed. Everything was rolled back safely.", ex);
            }

            var richProjectData = await _syncExecutionService.FetchRichDataAsync<GetProject>(
                
                configKey: "ProjectList",
                syncParams: new Dictionary<string, string> { { "ProjectId", finalProjectData.Id.ToString() } },
                matchPredicate: p => p.Id == finalProjectData.Id,
                fallbackData: finalProjectData,
                lastSync: null // Optional: pass DateTimeOffset if your SP requires it
            );
            if (richProjectData != null)
            {
                try
                {
                    await _realtimeNotifier.BroadcastAsync(new RealtimeMessage
                    {
                        Entity = "ProjectList",
                        Action = "StatusUpdate",
                        Payload = richProjectData,
                        KeyField = "Id",
                        RepoKey = richProjectData.RepoKey,
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to broadcast project status update: {ex.Message}");
                }
            }

            return finalProjectData;
        }
    }   
}
