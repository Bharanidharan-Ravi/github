using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.BusinessLayer.Hub;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.DomainLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.ProjectModal;

namespace WGAPP.BusinessLayer.Repository.GithubRepository
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly IProjectService _projectService;
        private readonly INotificationService _notificationService;
        public ProjectRepository(IProjectService projectService, INotificationService notification)
        {
            _projectService = projectService;
            _notificationService = notification;
        }

        public async Task<List<GetProject>> GetProjMaster(Guid? clientId = null, Guid? repoId = null)
        {
            var response = await _projectService.GetProjMaster(clientId, repoId);
            return response;
        }
        public async Task<GetProject> PostProject(ProjectMaster project)
        {
            var response = await _projectService.PostProject(project);
            await _notificationService.ProjCreated(response);
            return response;
        }
    }
}
