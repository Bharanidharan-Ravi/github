using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace APIGateway.API.Swagger
{
    public class HeaderParameters : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Skip if [AllowAnonymous] is applied
            var allowAnonymous = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            // Also check controller-level [AllowAnonymous]
            var controllerActionDescriptor = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
            var controllerAllowAnonymous = controllerActionDescriptor?.ControllerTypeInfo
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), true)
                .Any() ?? false;

            // Skip if route contains "login"
            var isLoginRoute = context.ApiDescription.RelativePath.ToLower().Contains("login");

            if (allowAnonymous || controllerAllowAnonymous)
            {
                return; // Skip adding WG_token
            }

            // Add WG_token header
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<OpenApiParameter>();
            }

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "WG_token",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema
                {
                    Type = "string"
                }
            });
        }
    }
}


//using Microsoft.OpenApi.Models;
//using Swashbuckle.AspNetCore.SwaggerGen;
//using System.Collections.Generic;

//namespace DentalApp.API.Swagger
//{
//    public class HeaderParameters : IOperationFilter
//    {
//        public void Apply(OpenApiOperation operation, OperationFilterContext context)
//        {
//            if (operation.Parameters == null)
//            {
//                operation.Parameters = new List<OpenApiParameter>();
//            }
//            operation.Parameters.Add(new OpenApiParameter
//            {
//                Name = "WG_token",
//                In = ParameterLocation.Header,
//                Required = true,
//                Schema = new OpenApiSchema
//                {
//                    Type = "string"
//                }
//            });
//        }
//    }
//}