using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace APIGateway.Controllers
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/[controller]")]
    public class RepoController : ControllerBase
    {
        private readonly IRepoRepository _repo;
        public RepoController(IRepoRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("PostRepo")]
        public async Task<IActionResult> PostRepo ([FromBody] RepoWithClient repoWith)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(repoWith));
            var response = await _repo.PostRepo(repoWith.Login, repoWith.Client, repoWith.Repo);
            return Ok(ApiResponseHelper.Success(response, "Repository create successfully."));
        }
    }
}
