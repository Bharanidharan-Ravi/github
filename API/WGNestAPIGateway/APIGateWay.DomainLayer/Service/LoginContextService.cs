using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Utilities;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace APIGateWay.DomainLayer.Service
{
    public class LoginContextService : ILoginContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEnvironmentRoutingService _environmentRouting;

        public LoginContextService(IHttpContextAccessor httpContextAccessor, IEnvironmentRoutingService environmentRouting)
        {
            _httpContextAccessor = httpContextAccessor;
            _environmentRouting = environmentRouting;
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

        // DbName claim, mapped to the test DB when the request is X-Environment: Test.
        public string databaseName =>
            _environmentRouting.ResolveDatabaseName(User?.FindFirst("DbName")?.Value);

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

        // "Roles" claim = comma-separated effective roles, e.g. "4,2" for a Ticket Admin.
        // Tokens issued before this claim existed fall back to the single role.
        public IReadOnlyList<int> roles
        {
            get
            {
                var parsed = (User?.FindFirst("Roles")?.Value ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(v => int.TryParse(v, out var r) ? r : 0)
                    .Where(r => r > 0)
                    .ToList();

                if (role > 0 && !parsed.Contains(role)) parsed.Add(role);
                return parsed;
            }
        }

        public bool HasRole(int r) => roles.Contains(r);

        public bool HasAnyRole(IEnumerable<int> allowed) => allowed.Any(HasRole);

        public string JwtToken =>
            HttpContext?.Request.Headers["Authorization"]
                .FirstOrDefault()?.Replace("Bearer ", "");

        public string RequestPath =>
            HttpContext?.Request.Path.Value;
    }
}