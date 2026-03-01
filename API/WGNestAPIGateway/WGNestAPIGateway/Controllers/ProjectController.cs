using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.BusinessLayer.Repository;
using APIGateWay.ModalLayer.PostData;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectRepo _project;
        public ProjectController(IProjectRepo project)
        {
            _project = project;
        }

        [HttpPost("PostProject")]
        public async Task<IActionResult> PostProject([FromBody] ProjectDto projectDto)
        {
            var response = await _project.CreateProjectAsync(projectDto);
            return Ok(ApiResponseHelper.Success(response, "Project create successfully."));
        }

      // ─────────────────────────────────────────────────────────────────────
        // PUT /api/project/{id}
        // All roles — any role can update a project in their repo.
        // Role validation: RepoScopeHandler checked role + repo before this runs.
        // Scope: Repo_Id in body → validated directly.
        //        Repo_Id absent  → handler looked up entity by {id} from DB.
        // No sequence call — ProjectKey never changes on update.
        // ─────────────────────────────────────────────────────────────────────
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Request body is required." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Title is required." });

            var result = await _project.UpdateProjectAsync(id, dto);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PATCH /api/project/{id}/status
        // All roles — any role can change status of a project in their repo.
        // Body: { "Status": 2 }   (no Repo_Id required)
        //
        // How scope works here:
        //   RepoScopeHandler sees PUT/PATCH + no Repo_Id in body
        //   → calls GetProjectRepoIdAsync(id) to look up entity's Repo_Id from DB
        //   → validates against user's allowed repos
        //   → if fails → 403 before controller ever runs
        //
        // Only Status column is updated. EF sends:
        //   UPDATE ProjectMasters SET Status=@s, UpdatedAt=@t, UpdatedBy=@u WHERE Id=@id
        // ─────────────────────────────────────────────────────────────────────
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateProjectStatus(Guid id, [FromBody] UpdateStatusDto dto)
        {
            if (dto == null)
                return BadRequest(new { Code = "VALIDATION_ERROR", ErrorMessage = "Status is required." });

            var result = await _project.UpdateProjectStatusAsync(id, dto);
            return Ok(result);
        }
    }
}
