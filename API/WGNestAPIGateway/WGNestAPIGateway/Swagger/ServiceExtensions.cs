using Microsoft.OpenApi.Models;

namespace APIGateway.Swagger

{
    public static class ServiceExtensions
    {
        public static void AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "APIGateway",
                    Version = "v1"
                });

                // 🔥 JWT Bearer Definition
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
            });
        }
    }
}


//public static class ServiceExtensions
//{
//    public static void AddSwaggerDocumentation(this IServiceCollection services)
//    {
//        services.AddSwaggerGen(c =>
//        {
//            c.SwaggerDoc("v1", new OpenApiInfo { Title = "APIGateway", Version = "v1" });
//            c.OperationFilter<API.Swagger.HeaderParameters>();
//            c.AddSecurityRequirement(new OpenApiSecurityRequirement
//            {
//                {new OpenApiSecurityScheme
//                {
//                    Reference=new OpenApiReference
//                    {
//                        Type=ReferenceType.SecurityScheme,
//                        Id="Bearer"
//                    }
//                },
//                new string[]{}

//                }
//            });
//        });
//    }
//}




