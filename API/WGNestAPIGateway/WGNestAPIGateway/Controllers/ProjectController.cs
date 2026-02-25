using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
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
        }
}
