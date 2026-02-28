using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.Business_Layer.Auth
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RequireRoleAttribute : Attribute, IActionFilter
    {
        private readonly int[] _allowedRoles;

        public RequireRoleAttribute(params int[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Role was already decoded by HttpContextMiddleware
            var roleStr = context.HttpContext.Items["UserDetail:Role"]?.ToString();
            var hasRole = int.TryParse(roleStr, out var role);

            if (!hasRole)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    ErrorCode = 401,
                    ErrorMessage = "Authentication required."
                });
                return;
            }

            if (!_allowedRoles.Contains(role))
            {
                context.Result = new ObjectResult(new
                {
                    ErrorCode = 403,
                    ErrorMessage = $"Role {role} is not permitted to perform this action."
                })
                { StatusCode = 403 };
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
