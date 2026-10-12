using System.Net;
using APIGateWay.ModelLayer;
using APIGateWay.ModelLayer.ErrorException;
using APIGateWay.BusinessLayer.Helpers.log;
using static APIGateWay.ModelLayer.ErrorException.Exceptionlist;
using APIGateWay.BusinessLayer.Helpers.ilog;

namespace APIGateWay.Middelware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _env;

        public ErrorHandlingMiddleware(RequestDelegate next, IConfiguration configuration, IServiceProvider serviceProvider, IWebHostEnvironment env)
        {
            _next = next;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Proceed with the next middleware or request handling
                await _next(context);
            }
            catch (Exception ex)
            {
                // Handle exception globally
                await HandleExceptionAsync(context, ex);
            }
        }
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted) return;

            context.Items["CapturedError"] = exception.Message;
            context.Items["CapturedStackTrace"] = exception.StackTrace;
            if (exception.InnerException != null)
            {
                context.Items["CapturedInnerException"] = exception.InnerException.Message;
            }

            context.Response.Clear();
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = exception switch
            {
                Exceptionlist.DataNotFoundException => (int)HttpStatusCode.NotFound,
                Exceptionlist.InvalidDataException => (int)HttpStatusCode.BadRequest,
                Exceptionlist.LoginException => (int)HttpStatusCode.Unauthorized,
                Exceptionlist.UnauthorizedException => (int)HttpStatusCode.Unauthorized,
                Exceptionlist.ForbiddenException => (int)HttpStatusCode.Forbidden,
                UserAlreadyExistsException => (int)HttpStatusCode.Conflict,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var errorResponse = new ErrorResponse
            {
                ErrorCode = context.Response.StatusCode,
                ErrorMessage = exception.Message
            };

            // In Development, expose the real cause (e.g. the SQL error behind
            // "An error occurred while saving the entity changes").
            if (_env.IsDevelopment())
            {
                var inner = new List<string>();
                for (var e = exception.InnerException; e != null; e = e.InnerException)
                    inner.Add($"{e.GetType().Name}: {e.Message}");
                errorResponse.InnerExceptions = inner.Count > 0 ? inner : null;
                errorResponse.StackTrace = exception.ToString();
            }

            using var scope = _serviceProvider.CreateScope();
            //var logHelper = scope.ServiceProvider.GetRequiredService<IlogHelper>();

            //if (exception is Exceptionlist.LoginException)
            //    await logHelper.LogInvalidLoginAttempt(exception);
            //else
            //    await logHelper.LogExceptionAsync(exception);

            await context.Response.WriteAsJsonAsync(errorResponse);
        }
        
    }
}



