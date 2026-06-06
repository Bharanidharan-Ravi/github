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

            string env = request?.Headers["X-Environment"].ToString();

            if (string.IsNullOrEmpty(env))
            {
                env = request?.Query["env"].ToString();
            }

            string connectionName =
                (env == "Test")
                    ? "TestConnection"
                    : "DefaultConnection";

            var conn =
                _configuration.GetConnectionString(connectionName);

            Console.WriteLine($"User={request?.HttpContext?.User?.Identity?.Name}");
            Console.WriteLine($"Env={env}");
            Console.WriteLine($"ConnectionName={connectionName}");
            Console.WriteLine($"Conn={conn}");

            return conn;
        }
    }
}
