using APIGateWay.BusinessLayer.Helpers.token;
using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.BusinessLayer.Interface;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.ModalLayer;
using APIGateWay.ModalLayer.DTOs;
using APIGateWay.ModalLayer.GETData;
using System;
using System.Text.Json;
using APIGateWay.DomainLayer.CommonSevice;

namespace APIGateWay.BusinessLayer.Repository
{
    public class LoginRepository : ILoginRepository
    {
        private readonly ILoginService _loginService;
        private readonly TokenGeneration _tokenGeneration;
        private readonly DecodeHelpers _decodeHelpers;
        private readonly APIGateWayCommonService _service;

        public static readonly Dictionary<Guid, string> _activeJwtTokens = new Dictionary<Guid, string>();

        public LoginRepository(ILoginService loginService, TokenGeneration tokenGeneration, DecodeHelpers decodeHelpers, APIGateWayCommonService service)
        {
            _loginService = loginService;
            _tokenGeneration = tokenGeneration;
            _decodeHelpers = decodeHelpers;
            _service = service;
        }
        public async Task<GetUserList> RegisterUserAsync(RegisterRequestDto request)
        {
            return await _loginService.RegisterUserAsync(request);
        }

        public async Task<string> GetUserinfo(string username, string password, string deviceInfo)
        {
            var userList = await _loginService.GetUser(username, password, deviceInfo);

            if (userList == null || !userList.Any())
                throw new Exception("Invalid username or password");

            var user = userList.First();
            var attachmentJSON = user.Attachment_JSON;
            if(!string.IsNullOrEmpty(attachmentJSON))
            {
                using var doc=JsonDocument.Parse(attachmentJSON);
                var root=doc.RootElement;
                var first=root.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("relativepath", out var relPathEl) &&
                    relPathEl.ValueKind == JsonValueKind.String)
                {
                    var relativePath = relPathEl.GetString();
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        user.PreviewUrl = _service.GeneratePreviewUrl(relativePath);
                    }
                }
            }

            var token = _tokenGeneration.GenerateJwtToken(
                user.UserId,
                user.UserName,
                user.Role,
                user.DBName,
                user.Team.ToString(),
                user?.PreviewUrl
            );

            return token;
        }

        //public async Task<string> GetUserinfo(string username, string password, string deviceInfo)
        //{
        //    //var userList = await _LoginService.GetUser(username, password, deviceInfo);
        //    var userList = await _loginService.GetUser(username, password, deviceInfo);
        //    var userInfos = new List<UserInfo>();
        //    foreach (var user in userList)
        //    {
        //        var userInfo = new UserInfo
        //        {
        //            UserId = user.UserId,
        //            UserName = user.UserName,
        //            DBName = user.DBName,
        //            Status = user.Status,
        //            Role = user.Role,
        //        };

        //        Guid userid = userInfo.UserId;
        //        userInfo.JwtToken = _tokenGeneration.GenerateJwtToken(userid, userInfo.UserName, userInfo.Role, userInfo.DBName);
        //        _activeJwtTokens[userid] = userInfo.JwtToken;
        //        userInfos.Add(userInfo);
        //    }
        //    var serializedUserInfos = JsonSerializer.Serialize(userInfos);
        //    var encryptionUser = _decodeHelpers.EncryptUserInfo(serializedUserInfos);
        //    var firstUser = userList[0];
        //    return (serializedUserInfos);
        //}

        //public async Task<List<GetEmployee>> GetEmployeeMaster()
        //{
        //    var response = await _loginService.GetEmployeeMaster();
        //    return response;
        //}
    }
}
