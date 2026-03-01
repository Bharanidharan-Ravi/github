using APIGateWay.ModalLayer.GETData;
using APIGateWay.ModalLayer.PostData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Interface
{
    public interface IProjectRepo
    {
        Task<GetProject> CreateProjectAsync(ProjectDto projectDto);

        // Full update — title, description, attachments, dueDate, status
        // PUT /api/project/{id}
        // Repo scope already validated by RepoScopeHandler before this is called
        Task<GetProject> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto);

        // Status-only update — only Status column changes, nothing else
        // PATCH /api/project/{id}/status
        // Body: { "Status": 2 }  — no Repo_Id needed in body
        // RepoScopeHandler validated scope via entity lookup from DB
        Task<GetProject> UpdateProjectStatusAsync(Guid projectId, UpdateStatusDto dto);
    }
}
