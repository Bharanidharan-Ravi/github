using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.ProjectModal;

namespace WGAPP.Controllers.GithubController
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/tickets/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectRepository _projectRepo;
        public ProjectController(IProjectRepository projectRepo) 
        {
            _projectRepo = projectRepo;
        }

        [HttpGet("GetProjMaster")]
        public async Task<IActionResult> GetProjMaster (Guid? clientId = null, Guid? repoId = null)
        {
            var response = await _projectRepo.GetProjMaster(clientId, repoId);
            return Ok(response);
        }

        [HttpPost("PostProject")]
        public async Task<IActionResult> PostProject(ProjectMaster project)
        {
            var response = await _projectRepo.PostProject(project);
            return Ok(response);
        }
    }
}
