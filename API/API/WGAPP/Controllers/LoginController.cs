using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using WGAPP.ModelLayer;
using Microsoft.AspNetCore.Authorization;
using WGAPP.BusinessLayer.Interface;

namespace WGAPP.Controllers
{
    [EnableCors("AllowAll")]
    [ApiController]
    [Route("api/")]
    public class LoginController : ControllerBase
    {

        private readonly ILogger<LoginController> _logger;
        private readonly ILoginRepository _loginRepository;


        public LoginController(ILogger<LoginController> logger, ILoginRepository loginRepository)
        {
            _logger = logger;
            _loginRepository = loginRepository;
        }

        #region Login funtion

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> getuser(userLogin user)
        {
            var dbUser = await _loginRepository.GetUserinfo(user.UserName, user.Password, user.DeviceInfo);

            if (dbUser == null)
            {
                return NotFound("No valid user");
            }
            return dbUser;
        }
        #endregion
    }
}
