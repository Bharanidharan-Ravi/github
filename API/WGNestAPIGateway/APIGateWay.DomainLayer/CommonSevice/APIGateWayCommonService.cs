using System;
using System.Data;
using APIGateWay.DomainLayer.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using APIGateWay.ModelLayer.ErrorException;
using Microsoft.Extensions.DependencyInjection;

namespace APIGateWay.DomainLayer.CommonSevice
{
    public class APIGateWayCommonService
    {
        private readonly SqlConnection sqlConnection;
        private readonly IConfiguration _configuration;
        private readonly APIGatewayDBContext _dbContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public APIGateWayCommonService(APIGatewayDBContext dbContext, IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _scopeFactory = serviceScopeFactory;
        }

        public async Task<List<T>> ExecuteGetItemAsyc<T>(
           string storedProcedure,
           params SqlParameter[] parameters
       ) where T : class
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider
                    .GetRequiredService<APIGatewayDBContext>();  // ✅ FIXED

                var validProcedureNames = new[]
                {
                    "VALIDATEUSER",
                    "GetEmployeeMaster",
                    "GetAllProjData",
                    "GETALLREPO",
                    "GetIssuesByID",
                    "GETLABELMASTER",
                    "GetNextNumber",
                    "DashBoardTimesheetData",
                    "GETTHREADLIST",
                    "GetStatusMaster",
                    "getdailyplan"
                };

                if (!validProcedureNames.Contains(storedProcedure))
                    throw new ArgumentException("Invalid stored procedure name");

                var sqlCommand = $"EXEC {storedProcedure} " +
                    $"{string.Join(", ",
                        parameters.Select(p =>
                            $"{p.ParameterName} = @{p.ParameterName.TrimStart('@')}"
                        )
                    )}";

                return await dbContext.Set<T>()
                    .FromSqlRaw(sqlCommand, parameters)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(ex.Message);
            }
        }
        //public async Task<List<T>> ExecuteGetItemAsyc<T>(string StoredProcedure, params SqlParameter[] parameters) where T : class
        //{
        //    try
        //    {
        //        using var scope = _scopeFactory.CreateScope();

        //        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        //        var validProcedureNames = new[] { "VALIDATEUSER","GETEMPLOYEEMASTER", "GetAllProjData", "GETALLREPO", "GetIssuesByID" };
        //        if (!validProcedureNames.Contains(StoredProcedure))
        //        {
        //            throw new ArgumentException("Invalid stored procedure name", nameof(StoredProcedure));
        //        }
        //        var sqlCommand = $"EXEC {StoredProcedure} " +
        //                  $"{string.Join(", ", parameters.Select(p => $"{p.ParameterName} = @{p.ParameterName.TrimStart('@')}"))}";

        //        return await dbContext.Set<T>()
        //            .FromSqlRaw(sqlCommand, parameters)
        //            .AsNoTracking()
        //            .ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the exception
        //        Console.Error.WriteLine($"An error occurred while executing the stored procedure: {ex.Message}");
        //        // Handle or rethrow the exception as needed
        //        throw new InvalidDataException(ex.Message);
        //    }
        //}
        public async Task<DataSet> ExecuteReturnAsync(string storedProcedureName, SqlParameter[] parameters)
        {
            var dataSet = new DataSet();

            try
            {
                // Create a connection to the database (e.g., SAP HANA or SQL Server)
                using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
                {
                    await connection.OpenAsync();

                    // Create a command to execute the stored procedure
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = storedProcedureName;
                        command.CommandType = CommandType.StoredProcedure;

                        // Add parameters to the command
                        foreach (var param in parameters)
                        {
                            var parameter = command.CreateParameter();
                            parameter.ParameterName = param.ParameterName;
                            parameter.Value = param.Value;
                            command.Parameters.Add(parameter);
                        }

                        // Execute the command asynchronously and fill the dataset
                        using (var dataAdapter = new SqlDataAdapter(command))
                        {
                            await Task.Run(() => dataAdapter.Fill(dataSet)); // Fill the dataset asynchronously
                        }
                    }
                }
                bool allTablesEmpty = dataSet.Tables.Count == 0 || dataSet.Tables.Cast<DataTable>().All(table => table.Rows.Count == 0);

                if (allTablesEmpty)
                {
                    throw new Exceptionlist.DataNotFoundException("No data found for the provided parameters.");
                }

                return dataSet; // Return the filled dataset
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing stored procedure: {ex.Message}");
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
            using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
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
