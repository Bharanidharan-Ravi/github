using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
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
        /// <summary>True when the request carries X-Environment: Test (or ?env=Test, used by SignalR).</summary>
        bool IsTestEnvironment { get; }

        string GetBaseConnectionString();

        /// <summary>
        /// The database a request should use for the login's DbName claim. In Test the live name is
        /// mapped through "TestEnvironment:DatabaseMap" (e.g. WG_APP -> WG_APP_TEST); an unmapped name
        /// falls back to TestConnection's database, so a Test request never reaches a live DB.
        /// </summary>
        string? ResolveDatabaseName(string? claimDatabaseName);
    }

    public class EnvironmentRoutingService : IEnvironmentRoutingService
    {
        public const string TEST = "Test";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public EnvironmentRoutingService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public bool IsTestEnvironment
        {
            get
            {
                var request = _httpContextAccessor.HttpContext?.Request;

                string env = request?.Headers["X-Environment"].ToString();

                if (string.IsNullOrEmpty(env))
                {
                    env = request?.Query["env"].ToString();
                }

                return string.Equals(env, TEST, StringComparison.OrdinalIgnoreCase);
            }
        }

        public string GetBaseConnectionString()
        {
            string connectionName =
                IsTestEnvironment
                    ? "TestConnection"
                    : "DefaultConnection";

            var conn =
                _configuration.GetConnectionString(connectionName);

            if (IsTestEnvironment && string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("X-Environment: Test was sent, but ConnectionStrings:TestConnection is not configured.");

            return conn;
        }

        public string? ResolveDatabaseName(string? claimDatabaseName)
        {
            if (!IsTestEnvironment) return claimDatabaseName;

            if (!string.IsNullOrWhiteSpace(claimDatabaseName))
            {
                var mapped = _configuration[$"TestEnvironment:DatabaseMap:{claimDatabaseName}"];
                if (!string.IsNullOrWhiteSpace(mapped)) return mapped;
            }

            return new SqlConnectionStringBuilder(GetBaseConnectionString()).InitialCatalog;
        }
    }
}
