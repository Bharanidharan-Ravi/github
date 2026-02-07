using APIGateWay.BusinessLayer.Helpers;
using APIGateWay.ModelLayer.ErrorException;

namespace APIGateway.Middleware
{
    public class HttpContextMiddleware
    {
        private readonly RequestDelegate _next;

        public HttpContextMiddleware(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task Invoke(HttpContext context, IConfiguration configuration)
        {

            var currentPath = context.Request.Path.Value.ToLower();
            context.Items["Request"] = context.Request.Path;

            if (!currentPath.Contains("index.html") && !currentPath.Contains("swagger") && !currentPath.Contains("favicon"))
            {
                var endpoint = context.GetEndpoint();
                var allowAnonymous = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null;
               
                if (!allowAnonymous)
                {
                    string token = null;

                    // Check custom header first
                    if (context.Request.Headers.ContainsKey("wg_token"))
                    {
                        token = context.Request.Headers["wg_token"];
                    }
                    // Then check standard Authorization header
                    else if (context.Request.Headers.ContainsKey("Authorization"))
                    {
                        var authHeader = context.Request.Headers["Authorization"].ToString();
                        if (authHeader.StartsWith("Bearer "))
                        {
                            token = authHeader.Substring("Bearer ".Length).Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Token is missing.");
                        return;
                    }

                    try
                    {
                        DecodeToken(token, configuration, context);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new Exceptionlist.UnauthorizedException(ex.Message);
                    }
                }
            }
            await _next(context); // Only called if everything above succeeds
        }

        private void DecodeToken(string token, IConfiguration configuration, HttpContext context)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Token is missing");
                throw new Exceptionlist.UnauthorizedException("Token is missing or empty.");
                //throw new UnauthorizedAccessException("Token is missing or empty.");
            }
            var currentPath = context.Request.Path.Value.ToLower();
            try
            {
                var decodeHelper = new DecodeHelpers(configuration);
                var decodedToken = decodeHelper.DecodeJwtToken(token);

                if (decodedToken != null)
                {
                    context.Items["UserDetail:UserName"] = decodedToken.UserName;
                    context.Items["UserDetail:USERID"] = decodedToken.UserId;
                    //configuration["UserDetail:ClientId"] = decodedToken.ClientId.ToString();
                    context.Items["UserDetail:Status"] = decodedToken.Status.ToString();
                    //configuration["UserDetail:Key"] = decodedToken.Key.ToString();
                    context.Items["UserDetail:DBName"] = decodedToken.DBName.ToString();
                    context.Items["UserDetail:Role"] = decodedToken.Role.ToString();
                    context.Items["jwtToken"] = decodedToken.JwtToken.ToString();
                    context.Items["Request"] = currentPath;
                }
                else
                {
                    throw new Exceptionlist.UnauthorizedException("Decoded token is null or invalid.");
                    //throw new UnauthorizedAccessException("Decoded token is null or invalid.");
                }
            }
            catch (Exception ex)
            {
                throw new Exceptionlist.UnauthorizedException(ex.Message);
                //throw new UnauthorizedAccessException("Token is invalid or expired.", ex);
            }
        }
    }
}
