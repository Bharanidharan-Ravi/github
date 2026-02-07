using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.ModelLayer.GithubModal.ProjectModal;

namespace WGAPP.BusinessLayer.Interface.GithubInterface
{
    public interface IProjectRepository
    {
        Task<List<GetProject>> GetProjMaster(Guid? clientId = null, Guid? repoId = null);
        Task<GetProject> PostProject(ProjectMaster project);
    }
}
