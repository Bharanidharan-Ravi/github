
//using MelwaProdApp.Helper;
using WGAPP.ModelLayer;
using WGAPP.BusinessLayer.Helpers;
using Newtonsoft.Json;
using WGAPP.DomainLayer.ErrorException;

namespace WGAPP.Middelware
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
            var folders = configuration.GetSection("StaticFolders").Get<List<StatciFolderItem>>() ?? new List<StatciFolderItem>();
            foreach (var folder in folders) 
            {
                if (!string.IsNullOrEmpty(folder.RequestPath) && currentPath.StartsWith(folder.RequestPath.ToLower()))
                {
                    await _next(context);
                    return;
                } 
            }

            // 🚫 Skip token validation for swagger, index, favicon
            if (!currentPath.Contains("index.html")
                && !currentPath.Contains("swagger")
                && !currentPath.Contains("favicon"))
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
                        //context.Response.StatusCode = 401;
                        //await context.Response.WriteAsync("Unauthorized: " + ex.Message);
                        //return;
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
                throw new UnauthorizedAccessException("Token is missing or empty.");
            }

            try
            {
                var decodeHelper = new DecodeHelpers(configuration);
                var decodedToken = decodeHelper.DecodeJwtToken(token);

                if (decodedToken != null)
                {
                    context.Items["UserDetail:UserName"] = decodedToken.UserName;
                    context.Items["UserDetail:USERID"] = decodedToken.UserId;
                    context.Items["UserDetail:ClientId"] = decodedToken.ClientId?.ToString();
                    context.Items["UserDetail:Status"] = decodedToken.Status.ToString();
                    //configuration["UserDetail:Key"] = decodedToken.Key.ToString();
                    context.Items["UserDetail:DBName"] = decodedToken.DBName.ToString();
                    context.Items["UserDetail:Role"] = decodedToken.Role.ToString();
                    context.Items["jwtToken"] = decodedToken.JwtToken.ToString();
                }
                else
                {
                    throw new UnauthorizedAccessException("Decoded token is null or invalid.");
                }
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Token is invalid or expired.", ex);
            }
        }


        //private void DecodeToken(string token, IConfiguration configuration, HttpContext context)
        //{
        //    if (string.IsNullOrEmpty(token))
        //    {
        //        // Optionally log the missing token error
        //        throw new UnauthorizedAccessException("Token is missing or empty.");
        //    }
        //    try
        //    {
        //        var decodeHelper = new DecodeHelpers(configuration);
        //        var decodedToken = decodeHelper.DecodeJwtToken(token);
        //        //var decodedToken = new DecodeHelper().DecodeJwtToken(token);
        //        Console.WriteLine(decodedToken); // Or log to file
        //        var tokenObj = decodedToken;

        //        // Make sure all properties are valid (in case some values are null or empty)
        //        if (tokenObj != null)
        //        {
        //            configuration["UserDetail:UserName"] = tokenObj.UserName;
        //            configuration["UserDetail:USERID"] = tokenObj.UserId.ToString();
        //            configuration["UserDetail:SalesEmpCode"] = tokenObj.SalesEmpCode.ToString();
        //            configuration["UserDetail:SalesEmpName"] = tokenObj.SalesEmpName ?? string.Empty;
        //            configuration["UserDetail:CompanyName"] = tokenObj.CompanyName ?? string.Empty;
        //            configuration["UserDetail:DatabaseName"] = tokenObj.DBName ?? string.Empty;
        //            configuration["UserDetail:CardCode"] = tokenObj.CardCode ?? string.Empty;
        //            configuration["UserDetail:CardName"] = tokenObj.CardName ?? string.Empty;
        //            configuration["UserDetail:BranchName"] = tokenObj.BranchName ?? string.Empty;
        //            configuration["UserDetail:BranchID"] = tokenObj.BranchID.ToString() ?? string.Empty;
        //            context.Items["jwtToken"] = tokenObj.JwtToken ?? string.Empty;
        //        }
        //        else
        //        {
        //            throw new UnauthorizedAccessException("Decoded token is null or invalid.");
        //        }
        //    }
        //    catch (JsonSerializationException ex)
        //    {
        //        throw new UnauthorizedAccessException("Error during token deserialization: " + ex.Message, ex);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new UnauthorizedAccessException("Token is invalid or expired.", ex);
        //    }
        //}
    }
}
