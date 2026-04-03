using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Helpers;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace eAdmin.Web.Controllers
{
    /// <summary>
    /// Xử lý đăng nhập, đăng xuất và quản lý tài khoản
    /// </summary>
    public class AccountController : Controller
    {
        private readonly IUnitOfWork _uow;
        public AccountController(IUnitOfWork uow) => _uow = uow;

        // ── GET /Account/Login ─────────────────────────────────────────
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ── POST /Account/Login ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            // Tìm user theo username
            var users = await _uow.Users.FindAsync(
                u => u.Username == model.Username && u.IsActive);

            var user = System.Linq.Enumerable.FirstOrDefault(users);

            if (user == null || !PasswordHelper.VerifyPassword(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Incorrect username or password.");
                return View(model);
            }

            // Lấy thông tin Role
            var role = await _uow.Roles.GetByIdAsync(user.RoleId);

            // Tạo Claims cho Cookie Authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name,           user.FullName),
                new Claim("Username",                user.Username),
                new Claim("Role",                    role?.RoleName ?? "Student"),
                new Claim("DepartmentId",            user.DepartmentId?.ToString() ?? ""),
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc   = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                principal, authProps);

            // Ghi audit log
            await _uow.AuditLogs.AddAsync(new Domain.Entities.AuditLog
            {
                UserId     = user.UserId,
                Action     = "Login",
                EntityType = "User",
                EntityId   = user.UserId
            });
            await _uow.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        // ── POST /Account/Logout ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // ── GET /Account/AccessDenied ──────────────────────────────────
        public IActionResult AccessDenied() => View();

        // ── GET /Account/Profile ───────────────────────────────────────
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user   = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return NotFound();

            var vm = new UserViewModel
            {
                UserId       = user.UserId,
                Username     = user.Username,
                FullName     = user.FullName,
                Email        = user.Email,
                Phone        = user.Phone,
                DepartmentId = user.DepartmentId
            };
            return View(vm);
        }
    }
}
