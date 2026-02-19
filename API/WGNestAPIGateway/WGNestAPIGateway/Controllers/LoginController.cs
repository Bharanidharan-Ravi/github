using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace WGNestAPIGateway.Controllers
{
    //[EnableCors("AllowAll")]
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly ILoginRepository _loginRepository;

        public LoginController(ILoginRepository loginRepository)
        {
           _loginRepository = loginRepository;
        }

        #region User Creation 
        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterUserAsync(RegisterRequestDto request)
        {
            var response = await _loginRepository.RegisterUserAsync(request);
            return Ok(ApiResponseHelper.Success(response, "User registered successfully."));
        }
        #endregion

        #region User Login method 
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> GetUserinfo(userLogin user)
        {
            var response = await _loginRepository.GetUserinfo(user.UserName, user.Password, user.DeviceInfo);
            return Ok(ApiResponseHelper.Success(response, "Login successfully."));
        }
        #endregion

        #region User Login method 
        [HttpPost("repoPost")]
        public async Task<IActionResult> postRepo(userLogin user)
        {
            var response = await _loginRepository.GetUserinfo(user.UserName, user.Password, user.DeviceInfo);
            return Ok(ApiResponseHelper.Success(response, "Login successfully."));
        }
        #endregion

        #region Get employee master 
        [HttpGet("GetEmployeeMaster")]
        public async Task<IActionResult> GetEmployeeMaster()
        {
            var response = await _loginRepository.GetEmployeeMaster();
            return Ok(ApiResponseHelper.Success(response));
        }
        #endregion
    }
}
