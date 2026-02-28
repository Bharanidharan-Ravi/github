using APIGateWay.DomainLayer.Interface;
using Microsoft.AspNetCore.Http;
using System;

namespace APIGateWay.DomainLayer.Service
{
    public class LoginContextService : ILoginContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoginContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext httpContext => _httpContextAccessor.HttpContext;

        public Guid userId
        {
            get
            {
                var userIdObj = httpContext.Items["UserDetail:USERID"];
                if (userIdObj != null && Guid.TryParse(userIdObj.ToString(), out var result))
                {
                    return result; // Successfully parsed, return the Guid
                }

                // Handle the case where parsing fails, for example, return a default Guid (Guid.Empty)
                return Guid.Empty;
            }
        }
        public string userName => httpContext.Items["UserDetail:UserName"]?.ToString();
        public string databaseName => httpContext.Items["UserDetail:DBName"]?.ToString();
        public string Status => httpContext.Items["UserDetail:Status"]?.ToString();
        // ── ADD THIS ──────────────────────────────────────────────────
        // HttpContextMiddleware already decoded the JWT and stored the role.
        // We simply read it. No decoding happens here.
        public int role =>
            int.TryParse(httpContext?.Items["UserDetail:Role"]?.ToString(), out var r)
                ? r : 0;
        public string JwtToken => httpContext.Items["jwtToken"]?.ToString();
        public string RequestPath => httpContext.Items["Request"]?.ToString();
    }
}
