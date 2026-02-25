using APIGateWay.DomainLayer.CommonSevice;
using APIGateWay.DomainLayer.Helpers;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.MasterData.APIGateWay.ModalLayer.MasterData;
using APIGateWay.ModalLayer.PostData;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Repository
{
    public class ProjectRepo
    {
        private readonly IProjectService _project;
        private readonly IDomainService _domainService;
        private readonly APIGateWayCommonService _commonService;
        private readonly IMapper _mapper;
        private readonly ILoginContextService _loginContext;
        private readonly IAttachmentService _attachmentService;
        public ProjectRepo(IProjectService project, IDomainService domainService, APIGateWayCommonService service,
            IMapper mapper, ILoginContextService loginContext, IAttachmentService attachmentService)
        {
            _project = project;
            _domainService = domainService;
            _commonService = service;
            _mapper = mapper;
            _loginContext = loginContext;
            _attachmentService = attachmentService;
        }

        public async Task<GetProject> CreateProjectAsync(ProjectDto projectDto)
        {
            ProcessedAttachmentResult attachmentResult = null;

            try
            {
                // 🔥 ALL of this runs inside the Domain Layer's SQL Transaction!
                return await _domainService.ExecuteInTransactionAsync(async () =>
                {
                    // 1. MAPPING
                    var projectMaster = _mapper.Map<ProjectMaster>(projectDto);
                    projectMaster.Id = Guid.NewGuid();
                    projectMaster.Status = 1;

                    // 2. GET SEQUENCE (Locks the table)
                    var seq = await _commonService.GetNextSequenceAsync(projectDto.RepoKey);
                    projectMaster.SiNo = seq.CurrentValue;
                    projectMaster.ProjectKey = $"P{seq.ColumnValue}";

                    // 3. FILE PROCESSING
                    string finalHtmlDescription = projectDto.Description;

                    if (projectDto.temp?.temps != null && projectDto.temp.temps.Any())
                    {
                        var permUserId = $"{_loginContext.userId}-{_loginContext.userName}";
                        var permFolder = $"{projectMaster.ProjectKey}-{projectDto.Title}";
                        var relativePath = $"{permUserId}/{permFolder}";

                        attachmentResult = await _attachmentService.ProcessAndCopyAttachmentsAsync(
                            projectDto.Description,
                            projectDto.temp.temps,
                            relativePath,
                            projectMaster.Id.ToString(),
                            "ProjectMaster"
                        );

                        finalHtmlDescription = attachmentResult.UpdatedHtml;
                    }

                    // 4. HTML TO PLAIN TEXT
                    projectMaster.HtmlDesc = finalHtmlDescription;
                    projectMaster.Description = HtmlUtilities.ConvertToPlainText(finalHtmlDescription);

                    // 5. SAVE TO DB (Just adds to DbContext)
                    await _domainService.SaveEntityWithAttachmentsAsync(projectMaster, attachmentResult?.Attachments);

                    // 6. SUCCESS CLEANUP
                    if (projectDto.temp?.temps != null && projectDto.temp.temps.Any())
                    {
                        await _attachmentService.CleanupTempFiles(projectDto.temp);
                    }

                    // Return the final data
                    return _mapper.Map<GetProject>(projectMaster);
                });
            }
            catch (Exception ex)
            {
                // 🔥 FILE ROLLBACK
                // The SQL Transaction is automatically rolled back by the Domain wrapper.
                // This block handles deleting the physical files from the hard drive.
                if (attachmentResult?.PermanentFilePathsCreated?.Any() == true)
                {
                    _attachmentService.RollbackPhysicalFiles(attachmentResult.PermanentFilePathsCreated);
                }

                throw new Exception("Project creation failed. Everything was rolled back safely.", ex);
            }
        }
    }
}
