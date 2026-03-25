using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAdmin.Web.Controllers
{
    [Authorize][AuthorizeRoles("Admin")]
    public class RoleController : Controller
    {
        private readonly IUnitOfWork _uow;
        public RoleController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var roles = await _uow.Roles.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var vm = roles.Select(r => new RoleViewModel
            {
                RoleId = r.RoleId, RoleName = r.RoleName, Description = r.Description,
                UserCount = users.Count(u => u.RoleId == r.RoleId)
            }).ToList();
            return View(vm);
        }

        [HttpGet] public IActionResult Create() => View(new RoleViewModel());

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RoleViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var exists = await _uow.Roles.FindAsync(r => r.RoleName == vm.RoleName);
            if (exists.Any()) { ModelState.AddModelError("RoleName", "Role name already exists."); return View(vm); }
            await _uow.Roles.AddAsync(new Role { RoleName = vm.RoleName, Description = vm.Description });
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Role created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var r = await _uow.Roles.GetByIdAsync(id);
            if (r == null) return NotFound();
            return View(new RoleViewModel { RoleId = r.RoleId, RoleName = r.RoleName, Description = r.Description });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RoleViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var r = await _uow.Roles.GetByIdAsync(vm.RoleId);
            if (r == null) return NotFound();
            r.RoleName = vm.RoleName; r.Description = vm.Description;
            _uow.Roles.Update(r);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Role updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var users = await _uow.Users.FindAsync(u => u.RoleId == id);
            if (users.Any()) { TempData["Error"] = "Cannot delete role with assigned users."; return RedirectToAction(nameof(Index)); }
            var r = await _uow.Roles.GetByIdAsync(id);
            if (r != null) { _uow.Roles.Remove(r); await _uow.SaveChangesAsync(); }
            TempData["Success"] = "Role deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
