using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;


namespace APIGateway.Infrastructure
{
    public static class JwtServiceExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var key = configuration["Jwt:Key"];

            services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(key)),

                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            if (!string.IsNullOrEmpty(accessToken) &&
                                context.HttpContext.Request.Path.StartsWithSegments("/realtime"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },
                        // 🔐 Token missing or invalid
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();

                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.ContentType = "application/json";

                            var response = JsonSerializer.Serialize(new
                            {
                                code = "UNAUTHORIZED",
                                message = "Token is missing or invalid"
                            });

                            await context.Response.WriteAsync(response);
                        },

                        // 🔐 Authenticated but not authorized (RepoScopeHandler failed)
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";

                            var response = JsonSerializer.Serialize(new
                            {
                                code = "ACCESS_DENIED",
                                message = "You are not allowed to access this resource"
                            });

                            await context.Response.WriteAsync(response);
                        }
                    };
                });

            return services;
        }
    }
}
