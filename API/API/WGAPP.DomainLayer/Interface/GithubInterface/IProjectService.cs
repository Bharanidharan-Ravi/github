using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.ProjectModal;

namespace WGAPP.DomainLayer.Interface.GithubInterface
{
    public interface IProjectService
    {
        Task<List<GetProject>> GetProjMaster(Guid? clientId = null, Guid? repoId = null, Guid? ProjId = null);
        Task<GetProject> PostProject(ProjectMaster project);
    }
}
