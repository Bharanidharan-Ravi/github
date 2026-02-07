using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WGAPP.DomainLayer.ErrorException;
using WGAPP.DomainLayer.Service.CommonService;
using WGAPP.BusinessLayer.Helpers.ilog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WGAPP.BusinessLayer.Helpers.log
{
    public class LogHelper : IlogHelper
    {
        private static WGAPPCommonService _commonService;
        private readonly IConfiguration _configuration;

        public LogHelper(WGAPPCommonService commonService, IConfiguration configuration)
        {
            _commonService = commonService;
            _configuration = configuration;
        }

        public async Task LogExceptionAsync(Exception ex)
        {
            try
            {
                //_commonService = new MelwaProdAppCommonService(_configuration);
                DateTime currentDateTime = DateTime.Now;
                DateTime truncatedDateTime = currentDateTime.AddMilliseconds(-currentDateTime.Millisecond);
                var parameters = new[]
                {
                new SqlParameter("@userid", int.Parse(_commonService.userId)),
                new SqlParameter("@username", _commonService.userName),
                new SqlParameter("@company", "TestCompany"),
                new SqlParameter("@database", "databaseName"),
                new SqlParameter("@message", ex.Message),
                new SqlParameter("@exception", ex.ToString()),
                new SqlParameter("@stacktrace", ex.StackTrace),
                new SqlParameter("@source", _configuration["Request"]),
                new SqlParameter("@exceptiontime", truncatedDateTime ),
                };

                // Execute the stored procedure INSERTUSERLOG
                var dataSet = await _commonService.ExecuteReturnAsync("InsertExceptionlog", parameters);

                // Ensure the dataset is not null and contains rows
                using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
                {
                    await connection.OpenAsync();

                    // Prepare your query to check the data
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM ""EXCEPTIONLOGS""
                        WHERE ""USERID"" = :userid
                        AND ""EXCEPTIONTIME"" = :exceptiontime";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = checkQuery;

                        // Add parameters to the command
                        command.Parameters.Add(new SqlParameter(":userid", int.Parse(_commonService.userId)));
                        command.Parameters.Add(new SqlParameter(":exceptiontime", truncatedDateTime)); // Or use any other unique identifier

                        // Execute the query and get the count result
                        var result = await command.ExecuteScalarAsync();

                        if (result != null && Convert.ToInt32(result) > 0)
                        {
                            Console.WriteLine("Exception successfully logged.");
                        }
                        else
                        {
                            Console.WriteLine("Data was not inserted into the table.");
                            // Log to file if insertion failed
                            LogToFile(_configuration, ex);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                // Handle general exceptions during the logging process and log to file
                Console.WriteLine($"Error while logging exception: {exception.Message}");
                LogToFile(_configuration, exception);
            }
        }

        public async Task LogInvalidLoginAttempt(Exception ex)
        {
            try
            {
                DateTime currentDateTime = DateTime.Now;
                DateTime truncatedDateTime = currentDateTime.AddMilliseconds(-currentDateTime.Millisecond);
                if (ex is Exceptionlist.LoginException loginException)
                {
                    // Extract the properties from LoginException
                    string username = loginException.Username;
                    string deviceInfo = loginException.DeviceInfo;
                    string Password = loginException.Password;

                    string formattedMessage = $"{ex.Message}, Password: {Password}, deviceInfo: {deviceInfo}";
                 

                    var parameters = new[]
                    {
                        new SqlParameter("@userid", int.Parse("0")),
                        new SqlParameter("@username", username),  // Using the extracted username
                        //new SqlParameter("@deviceInfo", deviceInfo),  // Adding device info
                        new SqlParameter("@company",""),
                        new SqlParameter("@database",  ""),
                        new SqlParameter("@message", formattedMessage),
                          new SqlParameter("@exception", ex.ToString()),
                        new SqlParameter("@stacktrace", ex.StackTrace),
                        new SqlParameter("@source", _configuration["Request"]),
                        new SqlParameter("@exceptiontime", truncatedDateTime),
                        //new SqlParameter("@statusCode", statusCode) // Adding status code for logging
                    };

                    // Execute the stored procedure INSERTUSERLOG
                    var dataSet = await _commonService.ExecuteReturnAsync("InsertExceptionlog", parameters);
                }
                // Ensure the dataset is not null and contains rows
                using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
                {
                    await connection.OpenAsync();

                    // Prepare your query to check the data
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM ""EXCEPTIONLOGS""
                        WHERE ""USERID"" = :userid
                        AND ""EXCEPTIONTIME"" = :exceptiontime";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = checkQuery;

                        // Add parameters to the command
                        command.Parameters.Add(new SqlParameter(":userid", int.Parse("0")));
                        command.Parameters.Add(new SqlParameter(":exceptiontime", truncatedDateTime)); // Or use any other unique identifier

                        // Execute the query and get the count result
                        var result = await command.ExecuteScalarAsync();

                        if (result != null && Convert.ToInt32(result) > 0)
                        {
                            Console.WriteLine("Exception successfully logged.");
                        }
                        else
                        {
                            Console.WriteLine("Data was not inserted into the table.");
                            // Log to file if insertion failed
                            LogToFile(_configuration, ex);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                // Handle general exceptions during the logging process and log to file
                Console.WriteLine($"Error while logging exception: {exception.Message}");
                LogToFile(_configuration, exception);
            }
        }
        private static void LogToFile(IConfiguration configuration, Exception ex)
        {
            string path = $"{configuration["Logging:Path"]}{DateTime.Now.ToShortDateString()}.txt";

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Create(path).Dispose();
                using (TextWriter tw = new StreamWriter(path))
                {
                    tw.WriteLine(new String('=', 15));
                    tw.WriteLine(DateTime.Now.ToString());
                    tw.WriteLine($"Message : {ex.Message}");
                    tw.WriteLine($"StackTrace : {ex.StackTrace}");
                }
            }
            else if (File.Exists(path))
            {
                using (StreamWriter tw = File.AppendText(path))
                {
                    tw.WriteLine(new string('=', 15));
                    tw.WriteLine(DateTime.Now.ToString());
                    tw.WriteLine($"Message : {ex.Message}");
                    tw.WriteLine($"StackTrace : {ex.StackTrace}");
                }
            }
        }

        public async Task SavePostingData(string module, string action, string postingdata, string response)
        {
            var parameters = new[]
                    {
                new SqlParameter("@userid", int.Parse(_commonService.userId)),
                new SqlParameter("@username", _commonService.userName),
                new SqlParameter("@company", "TestCompany"),
                new SqlParameter("@database",   "databaseName"),
                new SqlParameter("@module", module),
                new SqlParameter("@action", action),
                new SqlParameter("@postingData", postingdata),
                new SqlParameter("@postingResponse", response ),
                new SqlParameter("@postingTime", DateTime.Now)
                };
            var dataSet = await _commonService.ExecuteReturnAsync("INSERTPOSTINGLOG", parameters);
            //return dataSet; //return dataSet;
        }
    }
}
