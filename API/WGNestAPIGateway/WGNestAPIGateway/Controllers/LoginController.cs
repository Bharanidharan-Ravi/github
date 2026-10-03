using APIGateway.Controllers;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using static APIGateWay.BusinessLayer.Repository.LoginRepository;

namespace WGNestAPIGateway.Controllers
{
    //[EnableCors("AllowAll")]
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly ILoginRepository _loginRepository;
        private readonly ISyncRepositoryV2 _syncRepo;

        public LoginController(ILoginRepository loginRepository, ISyncRepositoryV2 syncRepo)
        {
           _loginRepository = loginRepository;
           _syncRepo = syncRepo;
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

        #region Logout session
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
        SessionDTO request)
        {
            await _loginRepository.LogoutSession(
                request.SessionId);

            return Ok();
        }
        #endregion
        // Every 30 s from each open app: keeps this session online and returns everyone's
        // presence (sync key "GetUserOnlineStatus"), so clients don't poll it separately.
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat(
        [FromBody] SessionDTO request)
        {
            await _loginRepository.UpdateHeartbeat(
                request.SessionId);

            return this.SyncResult(await _syncRepo.RunAsync("GetUserOnlineStatus"));
        }
    }
}
