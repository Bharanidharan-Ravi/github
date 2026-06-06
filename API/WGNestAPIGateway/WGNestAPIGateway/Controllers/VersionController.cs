using APIGateWay.Business_Layer.Interface;
using APIGateWay.BusinessLayer.Helpers;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;

namespace APIGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VersionController : ControllerBase
    {
        private readonly IVersionRepo _versionRepo;
        public VersionController(IVersionRepo versionRepo)
        {
            _versionRepo = versionRepo;
        }

        [HttpGet("app-version")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAppVersion()
        {
            var version = await _versionRepo.GetAppVersion();
            return Ok(ApiResponseHelper.Success(version));
        }

        [HttpPost("publish-version")]
        [AllowAnonymous]
        public async Task<IActionResult>
        PublishVersion()
        {
            await _versionRepo
                .PublishVersionAsync();

            return Ok();
        }

        [HttpGet("assembly-version")]
        [AllowAnonymous]
        public IActionResult GetAssemblyVersion()
        {
            return Ok(new
            {
                Version = Assembly
                    .GetEntryAssembly()?
                    .GetCustomAttribute<
                        AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion,

                AssemblyVersion = Assembly
                .GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Split('+')[0]
                ?? "1.0"
        });
        }
    }
}
