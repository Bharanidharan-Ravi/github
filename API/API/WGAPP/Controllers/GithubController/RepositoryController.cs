using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.RepositoryModal;
using WGAPP.ModelLayer.GithubModal.ViewIssues;

namespace WGAPP.Controllers.GithubController
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/tickets/[controller]")]
    public class RepositoryController : ControllerBase
    {
        private IRepositoryInterface _repositoryInterface;
        public RepositoryController(IRepositoryInterface repositoryInterface)
        {
            _repositoryInterface = repositoryInterface;
        }

        #region Getting all Repo for employee login 
        [HttpGet("GetAllRepoData")]
        public async Task<IActionResult> GetRepoData(string clientId = null)
        {
            var result = await _repositoryInterface.GetRepoData(clientId);
            return Ok(result);
        }
        #endregion

        #region Post Repository data
        [HttpPost("PostRepository")]
        [AllowAnonymous]
        public async Task<IActionResult> InsertOrUpdateRepository([FromBody] PostRepositoryModel data, string DbName)
        {
            var message = await _repositoryInterface.InsertOrUpdateRepository(data, DbName);
            return Ok(message);

        }
        #endregion
    }
}
