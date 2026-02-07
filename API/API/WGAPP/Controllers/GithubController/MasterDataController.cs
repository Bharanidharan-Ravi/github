using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WGAPP.BusinessLayer.Interface.GithubInterface;
using WGAPP.ModelLayer.GithubModal.MasterData;

namespace WGAPP.Controllers.GithubController
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/tickets/[controller]")]
    public class MasterDataController : ControllerBase
    {
        private readonly IMasterDataRepo _masterDataRepo;
        public MasterDataController(IMasterDataRepo masterDataRepo)
        {
            _masterDataRepo = masterDataRepo;
        }

        [HttpGet("GetClients")]
        public async Task<IActionResult> GetClients()
        {
            var result = await _masterDataRepo.GetClients();
            return Ok(result);
        }
        
        [HttpGet("GetLabels")]
        public async Task<IActionResult> GetLabels()
        {
            var result = await _masterDataRepo.GetLabels();
            return Ok(result);
        }
    }
}
