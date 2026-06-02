using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.Utilities
{
    public interface IEnvironmentRoutingService
    {
        string GetBaseConnectionString();
    }

    public class EnvironmentRoutingService : IEnvironmentRoutingService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public EnvironmentRoutingService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public string GetBaseConnectionString()
        {
            var request = _httpContextAccessor.HttpContext?.Request;

            // 1. Check HTTP Header (For standard API calls)
            string env = request?.Headers["X-Environment"].ToString();

            // 2. Check Query String (Required for SignalR WebSockets)
            if (string.IsNullOrEmpty(env))
            {
                env = request?.Query["env"].ToString();
            }

            // 3. Return the correct Master Connection String
            string connectionName = (env == "Test") ? "TestConnection" : "DefaultConnection";
            return _configuration.GetConnectionString(connectionName);
        }
    }
}
