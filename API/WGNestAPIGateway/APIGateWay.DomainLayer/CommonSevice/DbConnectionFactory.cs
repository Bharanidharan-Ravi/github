using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Utilities;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IEnvironmentRoutingService _envRouting;
        private readonly ILoginContextService _loginContext;

        public DbConnectionFactory(
            IEnvironmentRoutingService envRouting,
            ILoginContextService loginContext)
        {
            _envRouting = envRouting;
            _loginContext = loginContext;
        }

        public SqlConnection CreateConnection()
        {
            var baseConnectionString =
                _envRouting.GetBaseConnectionString();

            var builder =
                new SqlConnectionStringBuilder(baseConnectionString);

            if (!string.IsNullOrWhiteSpace(_loginContext.databaseName))
            {
                builder.InitialCatalog =
                    _loginContext.databaseName;
            }

            return new SqlConnection(builder.ConnectionString);
        }
    }
}
