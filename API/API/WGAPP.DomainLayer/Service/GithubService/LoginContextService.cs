using WGAPP.DomainLayer.Interface.GithubInterface;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.DomainLayer.Service.GithubService
{
    public class LoginContextService : ILoginContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoginContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext httpContext => _httpContextAccessor.HttpContext;

        //public string userId => httpContext.Items["UserDetail:USERID"]?.ToString();
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
        public string ClientId => httpContext.Items["UserDetail:ClientId"]?.ToString();
        public string Role => httpContext.Items["UserDetail:Role"]?.ToString();
        public string JwtToken => httpContext.Items["jwtToken"]?.ToString();
        public string RequestPath => httpContext.Items["Request"]?.ToString();
    }
}

