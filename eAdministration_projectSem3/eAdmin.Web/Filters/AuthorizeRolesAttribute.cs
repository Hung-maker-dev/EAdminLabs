using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eAdmin.Web.Filters
{
    /// <summary>
    /// Attribute kiểm tra Role người dùng đang đăng nhập.
    /// Sử dụng: [AuthorizeRoles("Admin", "HOD")]
    /// </summary>
    public class AuthorizeRolesAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;
        public AuthorizeRolesAttribute(params string[] roles) => _roles = roles;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var roleClaim = context.HttpContext.User.FindFirst("Role")?.Value;
            if (roleClaim == null || !System.Array.Exists(_roles, r => r == roleClaim))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
            base.OnActionExecuting(context);
        }
    }
}
