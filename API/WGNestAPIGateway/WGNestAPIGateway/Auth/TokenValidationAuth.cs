using APIGateWay.ModelLayer.ErrorException;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace APIGateway.Auth
{
    public class TokenValidationAuth
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenValidationAuth> _logger;
        public TokenValidationAuth(RequestDelegate next, IConfiguration configuration, ILogger<TokenValidationAuth> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }
        public static bool PathEndsWithSegment(PathString path, string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return false;

            var segments = path.Value?.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments == null || segments.Length == 0)
                return false;

            return segments[^1].Equals(segment, StringComparison.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context)
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
            if (context.Request.Path.StartsWithSegments("/api/TicketingContoller/GetMasterIssueData"))
            {
                await _next(context);
                return;
            }
            // ✅ 3. Allow webhook route explicitly


            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }
            var token = context.Items["jwtToken"] as string;
            var UserName = context.Items["UserDetail:UserName"] as string;
            //var UserId = context.Items["UserDetail:USERID"];
            if (token != null)
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
                    throw new Exceptionlist.LoginException("Token expired", UserName, "", ex.Message);
                    //context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    //await context.Response.WriteAsync("Token expired. Please login again.");
                }
                catch (SecurityTokenException ex)
                {
                    _logger.LogWarning("Invalid token: " + ex.Message);
                    throw new Exceptionlist.LoginException("Token expired", UserName, "", ex.Message);
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
