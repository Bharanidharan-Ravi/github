using APIGateWay.DomainLayer.Interface;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace APIGateWay.DomainLayer.Service
{
    public class LoginContextService : ILoginContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoginContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext HttpContext => _httpContextAccessor.HttpContext;

        private ClaimsPrincipal User => HttpContext?.User;

        public Guid userId
        {
            get
            {
                var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return Guid.TryParse(value, out var id) ? id : Guid.Empty;
            }
        }

        public string userName =>
            User?.FindFirst(ClaimTypes.Name)?.Value;

        public string databaseName =>
            User?.FindFirst("DbName")?.Value;   // custom claim

        public string Status =>
            User?.FindFirst("Status")?.Value;   // if exists in JWT

        public int role
        {
            get
            {
                var value = User?.FindFirst(ClaimTypes.Role)?.Value;
                return int.TryParse(value, out var r) ? r : 0;
            }
        }

        public string JwtToken =>
            HttpContext?.Request.Headers["Authorization"]
                .FirstOrDefault()?.Replace("Bearer ", "");

        public string RequestPath =>
            HttpContext?.Request.Path.Value;
    }
}