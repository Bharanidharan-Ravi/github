using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
//using SAPbobsCOM;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.Operations;
//using WGAPP.DomainLayer.Connection;
using WGAPP.DomainLayer.DBContext;
using Microsoft.EntityFrameworkCore.Storage;

namespace WGAPP.DomainLayer.Service.CommonService;

public class WGAPPCommonService

{
    public readonly string userId;
    public readonly string userName;
    public readonly string companyName;
    public readonly string databaseName;
    public readonly string SalesEmpCode;
    public readonly string SalesEmpName;
    public readonly string CardCode;
    public readonly string CardName;
    public readonly string BranchName;
    public readonly string BranchID;
    public readonly string WhsCode;
    public readonly string Role;
    private readonly SqlConnection sqlConnection;
    private readonly IConfiguration _configuration;
    private readonly WGAPPDbContext _dbContext;
    //private readonly string _connectionString;

    public WGAPPCommonService(WGAPPDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        userId = _configuration["UserDetail:USERID"];
        userName = _configuration["UserDetail:UserName"];
        companyName = _configuration["UserDetail:CompanyName"];
        databaseName = _configuration["UserDetail:DBName"];
        SalesEmpCode = _configuration["UserDetail:SalesEmpCode"];
        SalesEmpName = _configuration["UserDetail:SalesEmpName"];
        CardCode = _configuration["UserDetail:CardCode"];
        CardName = _configuration["UserDetail:CardName"];
        BranchName = _configuration["UserDetail:BranchName"];
        BranchID = _configuration["UserDetail:BranchID"];
        WhsCode = _configuration["UserDetail:WhsCode"];
        Role = _configuration["UserDetail:Role"];
    }
    public async Task<List<T>> ExecuteGetItemAsyc<T>(string StoredProcedure, params SqlParameter[] parameters) where T : class
    {
        try
        {
            var validProcedureNames = new[] { "GETISSUESDATA", "validateuser","GETALLPROJECTDATA","GETISSUSEBYID","GetNextNumber", "GETLABELMASTER", "GetIssuesByUserId", "GETALLREPO", "GetAllIssuesData","GETLABELMASTER", "GETTHREADLIST" };
            if (!validProcedureNames.Contains(StoredProcedure))
            {
                throw new ArgumentException("Invalid stored procedure name", nameof(StoredProcedure));
            }
            var sqlCommand = $"EXEC {StoredProcedure} " +
                      $"{string.Join(", ", parameters.Select(p => $"{p.ParameterName} = @{p.ParameterName.TrimStart('@')}"))}";

            return await _dbContext.Set<T>()
                .FromSqlRaw(sqlCommand, parameters)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            // Log the exception
            Console.Error.WriteLine($"An error occurred while executing the stored procedure: {ex.Message}");
            // Handle or rethrow the exception as needed
            throw new InvalidDataException(ex.Message);
        }
    }

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

            return dataSet; // Return the filled dataset
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing stored procedure: {ex.Message}");
            throw; 
        }
    }

    public DataSet GetDataSet(string sqlQuery, IDictionary<string, object> sqlParams)
    {
        var connectionString = _dbContext.Database.GetConnectionString();

        using (var sqlConnection = new SqlConnection(connectionString))
        using (var sqlCommand = new SqlCommand(sqlQuery, sqlConnection))
        {
            foreach (var param in sqlParams)
            {
                sqlCommand.Parameters.AddWithValue(param.Key, param.Value);
            }

            var dataSet = new DataSet();
            using (var adapter = new SqlDataAdapter(sqlCommand))
            {
                adapter.Fill(dataSet);
            }

            return dataSet;
        }
    }
    public async Task ExecuteNonModalAsync(string storedProcedureName, SqlParameter[] parameters)
    {
        var validProcedureNames = new[] { "InsertUserlog" };

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

    public async Task<int> GetNextValueAsync(string sequenceName)
    {
        var sql = $"SELECT NEXT VALUE FOR {sequenceName}";

        await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;

        if (_dbContext.Database.CurrentTransaction != null) { 
            command.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();
        }

        if (command.Connection.State != ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }
        var result = await command.ExecuteReaderAsync();

        int value = -1;

        if (result.Read())
        {
            value = result.GetInt32(0);
        }
        result.Close();

        return value;
    }
}
