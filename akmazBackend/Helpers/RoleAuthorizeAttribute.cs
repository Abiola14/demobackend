using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;

namespace AkmazBackend.Helpers
{
    // Usage: [RoleAuthorize("admin")] or [RoleAuthorize("admin","auditor")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RoleAuthorizeAttribute : Attribute, IActionFilter
    {
        private readonly string[] _allowedRoles;

        public RoleAuthorizeAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles ?? Array.Empty<string>();
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Read role from request header "Role"
            var roleHeader = context.HttpContext.Request.Headers["Role"].FirstOrDefault();

            if (string.IsNullOrEmpty(roleHeader))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Missing Role header" });
                return;
            }

            // If allowedRoles contains the role, allow; otherwise block
            if (!_allowedRoles.Contains(roleHeader, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "You are not authorized to perform this action" });
                return;
            }

            // else allowed -> continue
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Nothing to do after action
        }
    }
}
