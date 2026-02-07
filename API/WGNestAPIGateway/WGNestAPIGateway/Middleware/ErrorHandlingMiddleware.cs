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

        public ErrorHandlingMiddleware(RequestDelegate next, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _next = next;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
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

            context.Response.Clear();
            context.Response.ContentType = "application/json";

            context.Response.StatusCode = exception switch
            {
                Exceptionlist.DataNotFoundException => (int)HttpStatusCode.NotFound,
                Exceptionlist.InvalidDataException => (int)HttpStatusCode.BadRequest,
                Exceptionlist.LoginException => (int)HttpStatusCode.Unauthorized,
                Exceptionlist.UnauthorizedException => (int)HttpStatusCode.Unauthorized,
                UserAlreadyExistsException => (int)HttpStatusCode.Conflict,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var errorResponse = new ErrorResponse
            {
                ErrorCode = context.Response.StatusCode,
                ErrorMessage = exception.Message
            };

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



