
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using WGAPP.DomainLayer.DBContext;
using WGAPP.DomainLayer.Services.CommonServices;
using WGAPP.BusinessLayer.Helpers.token;
using WGAPP.BusinessLayer.Helpers;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.BusinessLayer.Interface;
using WGAPP.DomainLayer.Interface;
using WGAPP.ModelLayer;

namespace WGAPP.BusinessLayer.Repository
{
    public class LoginRepository : ILoginRepository
    {
        private readonly ILoginService _LoginService;
        private readonly DecodeHelpers _decodeHelpers;
        private readonly TokenGeneration _tokenGeneration;
        private readonly IConfiguration _configuration;
        private readonly WGAPPCommonService _CommonService;

        public static readonly Dictionary<Guid, string> _activeJwtTokens = new Dictionary<Guid, string>();
        public LoginRepository(TokenGeneration tokenGeneration, ILoginService loginService, IConfiguration configuration, DecodeHelpers decodeHelpers)
        {
            _LoginService = loginService;
            _decodeHelpers = decodeHelpers;
            _configuration = configuration;
            _tokenGeneration = tokenGeneration;
        }
        public async Task<string> GetUserinfo(string username, string password, string deviceInfo)
        {
            var userList = await _LoginService.GetUser(username, password, deviceInfo);
            var userInfos = new List<UserInfo>();
            foreach (var user in userList)
            {
                var userInfo = new UserInfo
                {
                    UserId = user.UserId,
                    UserName = user.UserName,
                    DBName = user.DBName,
                    ClientId = user.ClientId,
                    Status = user.Status,
                    Key = user.Key,
                    Role = user.Role,
                };

                Guid userid = userInfo.UserId;
                userInfo.JwtToken = _tokenGeneration.GenerateJwtToken(userid);
                _activeJwtTokens[userid] = userInfo.JwtToken;
                userInfos.Add(userInfo);
            }
            var serializedUserInfos = JsonSerializer.Serialize(userInfos);
            var encryptionUser = _decodeHelpers.EncryptUserInfo(serializedUserInfos);
            return encryptionUser;
        }
    }
}
