using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.Helpers;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IUnitOfWork _uow;
        public UserController(IUnitOfWork uow) => _uow = uow;

        //[AuthorizeRoles("Admin")]
        //public async Task<IActionResult> Index(string? role, int? deptId)
        //{
        //    var users = await _uow.Users.GetAllAsync();
        //    var roles = await _uow.Roles.GetAllAsync();
        //    var depts = await _uow.Departments.GetAllAsync();

        //    var vm = users.Select(u => new UserViewModel
        //    {
        //        UserId = u.UserId,
        //        Username = u.Username,
        //        FullName = u.FullName,
        //        Email = u.Email,
        //        Phone = u.Phone,
        //        RoleId = u.RoleId,
        //        DepartmentId = u.DepartmentId,
        //        IsActive = u.IsActive,

        //        // 🔥 map thủ công (QUAN TRỌNG)
        //        RoleName = roles.FirstOrDefault(r => r.RoleId == u.RoleId)?.RoleName ?? "",
        //        DeptName = depts.FirstOrDefault(d => d.DepartmentId == u.DepartmentId)?.DepartmentName ?? ""
        //    });

        //    // 🔥 FILTER SAU KHI MAP
        //    if (!string.IsNullOrEmpty(role))
        //        vm = vm.Where(u => u.RoleName == role);

        //    if (deptId.HasValue)
        //        vm = vm.Where(u => u.DepartmentId == deptId);

        //    ViewBag.Roles = roles;
        //    ViewBag.Departments = depts;
        //    ViewBag.FilterRole = role;
        //    ViewBag.FilterDept = deptId;

        //    return View(vm.ToList());
        //}

        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Index(string? role, int? deptId, string? search)
        {
            var users = await _uow.Users.GetAllAsync();
            var roles = await _uow.Roles.GetAllAsync();
            var depts = await _uow.Departments.GetAllAsync();

            var vm = users.Select(u => new UserViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                DepartmentId = u.DepartmentId,
                IsActive = u.IsActive,
                RoleName = roles.FirstOrDefault(r => r.RoleId == u.RoleId)?.RoleName ?? "",
                DeptName = depts.FirstOrDefault(d => d.DepartmentId == u.DepartmentId)?.DepartmentName ?? ""
            });

            // 🔥 SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                vm = vm.Where(u =>
                    u.Username.ToLower().Contains(search) ||
                    u.FullName.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search)
                );
            }

            // 🔥 FILTER
            if (!string.IsNullOrEmpty(role))
                vm = vm.Where(u => u.RoleName == role);

            if (deptId.HasValue)
                vm = vm.Where(u => u.DepartmentId == deptId);

            // giữ lại giá trị filter
            ViewBag.Roles = roles;
            ViewBag.Departments = depts;
            ViewBag.FilterRole = role;
            ViewBag.FilterDept = deptId;
            ViewBag.Search = search;

            return View(vm.ToList());
        }

        [HttpGet][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new UserViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }

            var existing = await _uow.Users.FindAsync(u => u.Username == vm.Username || u.Email == vm.Email);
            if (existing.Any())
            {
                ModelState.AddModelError("", "Username or Email already exists.");
                await PopulateDropdowns(); return View(vm);
            }

            var user = new User
            {
                Username = vm.Username, FullName = vm.FullName, Email = vm.Email,
                Phone = vm.Phone, RoleId = vm.RoleId, DepartmentId = vm.DepartmentId,
                IsActive = vm.IsActive, CreatedAt = DateTime.UtcNow,
                PasswordHash = PasswordHelper.HashPassword(vm.Password ?? "123456")
            };
            await _uow.Users.AddAsync(user);
            await WriteAuditAsync("CreateUser", "User", 0, $"Created user {vm.Username}");
            await _uow.SaveChangesAsync();
            TempData["Success"] = "User created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return NotFound();
            await PopulateDropdowns();
            return View(new UserViewModel
            {
                UserId = user.UserId, Username = user.Username, FullName = user.FullName,
                Email = user.Email, Phone = user.Phone, RoleId = user.RoleId,
                DepartmentId = user.DepartmentId, IsActive = user.IsActive
            });
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(UserViewModel vm)
        {
            vm.Password = "placeholder"; // skip password validation on edit
            ModelState.Remove("Password");
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }

            var user = await _uow.Users.GetByIdAsync(vm.UserId);
            if (user == null) return NotFound();
            
            user.FullName = vm.FullName; user.Email = vm.Email; user.Phone = vm.Phone; user.Username = vm.Username;
            user.RoleId = vm.RoleId; user.DepartmentId = vm.DepartmentId; user.IsActive = vm.IsActive;
            _uow.Users.Update(user);
            await WriteAuditAsync("EditUser", "User", user.UserId, $"Updated user {user.Username}");
            await _uow.SaveChangesAsync();
            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return NotFound();
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            _uow.Users.Update(user);
            await WriteAuditAsync("ResetPassword", "User", user.UserId, $"Password reset for {user.Username}");
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Password reset successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _uow.Users.GetByIdAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            _uow.Users.Update(user);
            await WriteAuditAsync(user.IsActive ? "ActivateUser" : "DeactivateUser", "User", user.UserId, "");
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"User {(user.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Index));
        }

        // Profile & Change Password for current user
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var user = await _uow.Users.GetByIdAsync(userId);
            var roles = await _uow.Roles.GetAllAsync();
            var depts = await _uow.Departments.GetAllAsync();

            if (user == null) return NotFound();

            return View(new UserViewModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                RoleId = user.RoleId,
                DepartmentId = user.DepartmentId,

                // 🔥 map thủ công (giống Index)
                RoleName = roles.FirstOrDefault(r => r.RoleId == user.RoleId)?.RoleName ?? "",
                DeptName = depts.FirstOrDefault(d => d.DepartmentId == user.DepartmentId)?.DepartmentName ?? ""
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserViewModel vm)
        {
            // bỏ validation không cần
            ModelState.Remove("Password");
            ModelState.Remove("RoleId");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _uow.Users.GetByIdAsync(userId);

            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                // 🔥 load lại Role + Department để không mất dữ liệu
                var roles = await _uow.Roles.GetAllAsync();
                var depts = await _uow.Departments.GetAllAsync();

                vm.RoleName = roles.FirstOrDefault(r => r.RoleId == user.RoleId)?.RoleName ?? "";
                vm.DeptName = depts.FirstOrDefault(d => d.DepartmentId == user.DepartmentId)?.DepartmentName ?? "";

                return View(vm);
            }

            // 🔥 update
            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.Phone = vm.Phone;

            _uow.Users.Update(user);
            await _uow.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully.";

            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user == null) return NotFound();
            if (!PasswordHelper.VerifyPassword(vm.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(vm);
            }
            user.PasswordHash = PasswordHelper.HashPassword(vm.NewPassword);
            _uow.Users.Update(user);
            await WriteAuditAsync("ChangePassword", "User", userId, "");
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Roles = new SelectList(await _uow.Roles.GetAllAsync(), "RoleId", "RoleName");
            ViewBag.Departments = new SelectList(await _uow.Departments.GetAllAsync(), "DepartmentId", "DepartmentName");
        }

        private async Task WriteAuditAsync(string action, string entityType, int entityId, string details)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _uow.AuditLogs.AddAsync(new Domain.Entities.AuditLog
            { UserId = uid, Action = action, EntityType = entityType, EntityId = entityId, Details = details });
        }
    }
}
