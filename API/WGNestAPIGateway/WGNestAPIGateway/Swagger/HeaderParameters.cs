using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace APIGateway.API.Swagger
{
    public class HeaderParameters : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // ✅ Fix 1: Generic GetCustomAttribute<T>() works directly on MethodInfo
            var allowAnonymous = context.MethodInfo
                .GetCustomAttribute<AllowAnonymousAttribute>() != null;

            // ✅ Fix 2: Generic GetCustomAttribute<T>() works directly on TypeInfo
            var controllerActionDescriptor = context.ApiDescription.ActionDescriptor
                as ControllerActionDescriptor;

            var controllerAllowAnonymous = controllerActionDescriptor?.ControllerTypeInfo
                .GetCustomAttribute<AllowAnonymousAttribute>() != null;

            if (allowAnonymous || controllerAllowAnonymous)
            {
                return; // Skip adding WG_token header
            }

            operation.Parameters ??= new List<OpenApiParameter>();

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