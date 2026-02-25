using APIGateWay.ModelLayer.ErrorException;
using APIGateWay.BusinessLayer.Helpers.ilog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using APIGateWay.DomainLayer.Interface;
using APIGateWay.DomainLayer.CommonSevice;

namespace APIGateWay.BusinessLayer.Helpers.log
{
    public class LogHelper : IlogHelper
    {
        private static APIGateWayCommonService _commonService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly ILoginContextService _loginContextService;

        public LogHelper(APIGateWayCommonService commonService
            , IConfiguration configuration
            , IHttpContextAccessor contextAccessor
            , ILoginContextService loginContextService
            )
        {
            _commonService = commonService;
            _loginContextService = loginContextService;
            _configuration = configuration;
            _contextAccessor = contextAccessor;
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
                new SqlParameter("@userid", _loginContextService.userId),
                new SqlParameter("@username", _loginContextService.userName),
                new SqlParameter("@database", _loginContextService.databaseName),
                new SqlParameter("@message", ex.Message),
                new SqlParameter("@exception", ex.ToString()),
                new SqlParameter("@stacktrace", ex.StackTrace),
                new SqlParameter("@source", _loginContextService.RequestPath),
                new SqlParameter("@exceptiontime", truncatedDateTime ),
                };

                // Execute the stored procedure INSERTUSERLOG
                var dataSet = await _commonService.ExecuteReturnAsync("INSERTEXCEPTIONLOG", parameters);

                // Ensure the dataset is not null and contains rows
                using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
                {
                    await connection.OpenAsync();

                    // Prepare your query to check the data
                    string checkQuery = @"
                        SELECT COUNT(*) 
                        FROM EXCEPTIONLOG
                        WHERE Userid = @userid
                        AND Exceptiontime = @exceptiontime";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = checkQuery;

                        command.Parameters.Add(new SqlParameter("@userid", _loginContextService.userId));
                        command.Parameters.Add(new SqlParameter("@exceptiontime", truncatedDateTime)); // Or use any other unique identifier

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
                    string requestPath = _loginContextService.RequestPath;
                    string formattedMessage = $"{ex.Message}, Password: {Password}, deviceInfo: {deviceInfo}";


                    var parameters = new[]
                    {
                        new SqlParameter("@userid", loginException.UserId),
                        new SqlParameter("@username", username),
                        new SqlParameter("@database",  ""),
                        new SqlParameter("@message", formattedMessage),
                        new SqlParameter("@exception", ex.ToString()),
                        new SqlParameter("@stacktrace", ex.StackTrace),
                        new SqlParameter("@source", requestPath),
                        new SqlParameter("@exceptiontime", truncatedDateTime),
                    };

                    // Execute the stored procedure INSERTUSERLOG
                    var dataSet = await _commonService.ExecuteReturnAsync("INSERTEXCEPTIONLOG", parameters);
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
                new SqlParameter("@userid", _loginContextService.userId),
                new SqlParameter("@username", _loginContextService.userName),
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
