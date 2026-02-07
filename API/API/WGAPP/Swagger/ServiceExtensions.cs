using Microsoft.OpenApi.Models;

namespace WGAPP.Swagger

{
    public static class ServiceExtensions
    {
        public static void AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "WGApp", Version = "v1" });
                c.OperationFilter<API.Swagger.HeaderParameters>();
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {new OpenApiSecurityScheme
                    {
                        Reference=new OpenApiReference
                        {
                            Type=ReferenceType.SecurityScheme,
                            Id="Bearer"
                        }
                    },
                    new string[]{}

                    }
                });
            });
        }
    }
}
  


    
