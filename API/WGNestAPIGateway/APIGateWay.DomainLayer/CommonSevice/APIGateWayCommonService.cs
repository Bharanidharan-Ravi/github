using System;
using System.Data;
using APIGateWay.DomainLayer.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.Utilities;
using Microsoft.AspNetCore.Connections;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public class APIGateWayCommonService
    {
        private readonly APIGatewayDBContext _dbContext;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDbConnectionFactory _connectionFactory;

        public APIGateWayCommonService(APIGatewayDBContext dbContext,
        IServiceScopeFactory scopeFactory,
        IDbConnectionFactory connectionFactory)
        {
            _dbContext = dbContext;
            _scopeFactory = scopeFactory;
            _connectionFactory = connectionFactory;
        }

        public async Task<List<T>> ExecuteGetItemAsyc<T>(
        string storedProcedure,
        params SqlParameter[] parameters)
        where T : class
        {
            try
            {
                using var scope =
                    _scopeFactory.CreateScope();

                var dbContext =
                    scope.ServiceProvider
                         .GetRequiredService<APIGatewayDBContext>();

                var sqlCommand =
                    $"EXEC {storedProcedure} " +
                    string.Join(
                        ", ",
                        parameters.Select(
                            p =>
                            $"{p.ParameterName} = @{p.ParameterName.TrimStart('@')}"
                        ));

                return await dbContext
                    .Set<T>()
                    .FromSqlRaw(sqlCommand, parameters)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(ex.Message);
            }
        }
        public async Task<DataSet> ExecuteReturnAsync(
            string storedProcedureName,
            SqlParameter[] parameters)
        {
            var dataSet = new DataSet();

            try
            {
                using var connection =
                    _connectionFactory.CreateConnection();

                await connection.OpenAsync();

                using var command =
                    new SqlCommand(
                        storedProcedureName,
                        connection);

                command.CommandType =
                    CommandType.StoredProcedure;

                if (parameters?.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                using var adapter =
                    new SqlDataAdapter(command);

                await Task.Run(() =>
                    adapter.Fill(dataSet));

                bool allTablesEmpty =
                    dataSet.Tables.Count == 0 ||
                    dataSet.Tables
                           .Cast<DataTable>()
                           .All(t => t.Rows.Count == 0);

                if (allTablesEmpty)
                {
                    throw new Exceptionlist.DataNotFoundException(
                        "No data found for the provided parameters.");
                }

                return dataSet;
            }
            catch
            {
                throw;
            }
        }
        public async Task ExecuteNonModalAsync(string storedProcedureName, SqlParameter[] parameters)
        {
            var validProcedureNames = new[] { "INSERTUSERLOG" };

            if (!validProcedureNames.Contains(storedProcedureName))
            {
                throw new ArgumentException("Invalid stored procedure name", (storedProcedureName));
            }
            //using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
            // This automatically inherits the Live/Test routing AND the Tenant DB routing
            using (var connection = new SqlConnection(_dbContext.Database.GetDbConnection().ConnectionString))
            {
                await connection.OpenAsync();

                // Create a HanaCommand for executing the stored procedure
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = storedProcedureName;
                    command.CommandType = CommandType.StoredProcedure;

                    // Add parameters to the command
                    foreach (var parameter in parameters)
                    {
                        command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
                    }

                    // Execute the non-query command (used for insert, update, delete operations)
                    await command.ExecuteNonQueryAsync();
                }
            }

        }
    }
}
