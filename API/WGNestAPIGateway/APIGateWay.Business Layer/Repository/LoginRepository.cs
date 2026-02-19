using APIGateWay.BusinessLayer.Helpers.token;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Service;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace APIGateWay.BusinessLayer.Repository
{
    public class LoginRepository : ILoginRepository
    {
        private readonly ILoginService _loginService;
        private readonly TokenGeneration _tokenGeneration;
        private readonly DecodeHelpers _decodeHelpers;

        public static readonly Dictionary<Guid, string> _activeJwtTokens = new Dictionary<Guid, string>();

        public LoginRepository(ILoginService loginService, TokenGeneration tokenGeneration, DecodeHelpers decodeHelpers)
        {
            _loginService = loginService;
            _tokenGeneration = tokenGeneration;
            _decodeHelpers = decodeHelpers;
        }
        public async Task<GetUserList> RegisterUserAsync(RegisterRequestDto request)
        {
            return await _loginService.RegisterUserAsync(request);
        }

        public async Task<string> GetUserinfo(string username, string password, string deviceInfo)
        {
            //var userList = await _LoginService.GetUser(username, password, deviceInfo);
            var userList = await _loginService.GetUser(username, password, deviceInfo);
            var userInfos = new List<UserInfo>();
            foreach (var user in userList)
            {
                var userInfo = new UserInfo
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    DBName = user.DBName,
                    Status = user.Status,
                    Role = user.Role,
                };

                Guid userid = userInfo.UserId;
                userInfo.JwtToken = _tokenGeneration.GenerateJwtToken(userid, userInfo.UserName, userInfo.Role);
                _activeJwtTokens[userid] = userInfo.JwtToken;
                userInfos.Add(userInfo);
            }
            var serializedUserInfos = JsonSerializer.Serialize(userInfos);
            var encryptionUser = _decodeHelpers.EncryptUserInfo(serializedUserInfos);
            var firstUser = userList[0];
            return (encryptionUser);
        }

        public async Task<List<GetEmployee>> GetEmployeeMaster()
        {
            var response = await _loginService.GetEmployeeMaster();
            return response;
        }
    }
}
