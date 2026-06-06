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
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using APIGateWay.Business_Layer.Session;
using APIGateWay.DomainLayer.Utilities;

namespace APIGateWay.BusinessLayer.Repository
{
    public class LoginRepository : ILoginRepository
    {
        private readonly ILoginService _loginService;
        private readonly TokenGeneration _tokenGeneration;
        private readonly DecodeHelpers _decodeHelpers;
        private readonly APIGateWayCommonService _service;
        private readonly GenerateHelper _helper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISessionTrackingService _sessionTracking;
        private readonly INotificationRepository _notificationRepository;

        public static readonly Dictionary<Guid, string> _activeJwtTokens = new Dictionary<Guid, string>();
 
        public class SessionDTO
        {
            public Guid SessionId { get; set; }
        }
        public LoginRepository(ILoginService loginService, TokenGeneration tokenGeneration, DecodeHelpers decodeHelpers, APIGateWayCommonService service, IHttpContextAccessor httpContextAccessor, 
            ISessionTrackingService sessionTracking, INotificationRepository notificationRepository, GenerateHelper helper)
        {
            _loginService = loginService;
            _tokenGeneration = tokenGeneration;
            _decodeHelpers = decodeHelpers;
            _service = service;
            _httpContextAccessor = httpContextAccessor;
            _sessionTracking = sessionTracking;
            _notificationRepository = notificationRepository;
            _helper = helper;
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
                        user.PreviewUrl = _helper.GeneratePreviewUrl(relativePath);
                    }
                }
            }
            var sessionId = Guid.NewGuid();
            var jwtId = Guid.NewGuid();

            var indiaTimeZone =
              TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            var indiaTime =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    indiaTimeZone
                );


            var tokenIssuedAt = indiaTime;
            var tokenExpiresAt = tokenIssuedAt.AddDays(1);
            var browserInfo = deviceInfo;
            var ip =
             _httpContextAccessor.HttpContext?
             .Request.Headers["X-Forwarded-For"]
             .FirstOrDefault()
             ??
             _httpContextAccessor.HttpContext?
             .Connection.RemoteIpAddress?
             .ToString();
            var parameters = new[]
           {
                new SqlParameter("@SessionId", sessionId),
                new SqlParameter("@UserId", user.UserId),
                new SqlParameter("@JwtId", jwtId),

                new SqlParameter("@TokenIssuedAt", tokenIssuedAt),
                new SqlParameter("@TokenExpiresAt", tokenExpiresAt),

                new SqlParameter("@DeviceInfo",
                    (object?)deviceInfo ?? DBNull.Value),

                new SqlParameter("@BrowserInfo",
                    (object?)browserInfo ?? DBNull.Value),

                new SqlParameter("@IpAddress",
                    (object?)ip ?? DBNull.Value)
            };
            await _service.ExecuteReturnAsync(
                "USP_CreateUserSession",
                parameters
            );
            await _notificationRepository
            .EnsureUserStateAsync(
                 user.UserId);

            var token =
            _tokenGeneration.GenerateJwtToken(
                user.UserId,
                user.UserName,
                user.Role,
                user.DBName,
                user.Team?.ToString(),
                user.PreviewUrl,

                sessionId,
                jwtId,
                tokenIssuedAt,
                tokenExpiresAt
            );

           
           
            return token;
        }
        public async Task LogoutSession(Guid sessionId)
        {
            await _service.ExecuteReturnAsync(
                "USP_LogoutSession",
                new[]
                {
            new SqlParameter("@SessionId", sessionId),
            new SqlParameter("@LogoutReason", "ManualLogout")
                }
            );
        }

        public async Task UpdateHeartbeat(Guid sessionId)
        {
            await _service.ExecuteReturnAsync(
                "USP_UpdateHeartbeat",
                new[]
                {
            new SqlParameter("@SessionId", sessionId)
                });
        }
    }
}
