using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGateWay.BusinessLayer.Auth
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RequireRoleAttribute : Attribute, IActionFilter
    {
        private readonly int[] _allowedRoles;

        public RequireRoleAttribute(params int[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var roleStr = context.HttpContext.Items["UserDetail:Role"]?.ToString();

            if (!int.TryParse(roleStr, out var role))
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
                    ErrorMessage = "You do not have permission to perform this action."
                })
                { StatusCode = 403 };
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
