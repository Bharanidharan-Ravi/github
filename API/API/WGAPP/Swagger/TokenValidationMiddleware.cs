using System.IdentityModel.Tokens.Jwt;
using System.Text;
using WGAPP.DomainLayer.ErrorException;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace WGAPP.Swagger
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenValidationMiddleware> _logger;
        public TokenValidationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context )
        {
            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                _logger.LogWarning("Endpoint is null");
            }
            else
            {
                _logger.LogWarning($"Endpoint :{endpoint.DisplayName}");
            }
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }
            var token = context.Items["jwtToken"] as string;
            if(token != null)
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
                try
                {
                    tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    }, out SecurityToken validatedToken);
                    await _next(context);
                }
                catch (SecurityTokenExpiredException ex)
                {
                    _logger.LogWarning("Token expired: " + ex.Message);
                    throw new Exceptionlist.LoginException("Token expired", "", "", ex.Message);
                    //context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    //await context.Response.WriteAsync("Token expired. Please login again.");
                }
                catch (SecurityTokenException ex)
                {
                    _logger.LogWarning("Invalid token: " + ex.Message);
                    throw new Exceptionlist.LoginException("Token expired", "", "", ex.Message);
                    //context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    //await context.Response.WriteAsync("Invalid token.");
                }

            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token is Required");
            }

        }
    }
}
