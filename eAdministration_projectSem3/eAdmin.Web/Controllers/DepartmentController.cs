using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eAdmin.Web.Controllers
{
    [Authorize][AuthorizeRoles("Admin")]
    public class DepartmentController : Controller
    {
        private readonly IUnitOfWork _uow;
        public DepartmentController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var depts = await _uow.Departments.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var vm = depts.Select(d => new DepartmentViewModel
            {
                DepartmentId = d.DepartmentId, DepartmentName = d.DepartmentName, Code = d.Code,
                HodUserId = d.HodUserId,
                HodName = d.HodUser != null ? d.HodUser.FullName : (users.FirstOrDefault(u => u.UserId == d.HodUserId)?.FullName ?? ""),
                UserCount = users.Count(u => u.DepartmentId == d.DepartmentId)
            }).ToList();
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateHodList();
            return View(new DepartmentViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateHodList(); return View(vm); }
            await _uow.Departments.AddAsync(new Department { DepartmentName = vm.DepartmentName, Code = vm.Code, HodUserId = vm.HodUserId });
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Department created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var d = await _uow.Departments.GetByIdAsync(id);
            if (d == null) return NotFound();
            await PopulateHodList();
            return View(new DepartmentViewModel { DepartmentId = d.DepartmentId, DepartmentName = d.DepartmentName, Code = d.Code, HodUserId = d.HodUserId });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DepartmentViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateHodList(); return View(vm); }
            var d = await _uow.Departments.GetByIdAsync(vm.DepartmentId);
            if (d == null) return NotFound();
            d.DepartmentName = vm.DepartmentName; d.Code = vm.Code; d.HodUserId = vm.HodUserId;
            _uow.Departments.Update(d);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Department updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var users = await _uow.Users.FindAsync(u => u.DepartmentId == id);
            if (users.Any()) { TempData["Error"] = "Cannot delete department with assigned users."; return RedirectToAction(nameof(Index)); }
            var d = await _uow.Departments.GetByIdAsync(id);
            if (d != null) { _uow.Departments.Remove(d); await _uow.SaveChangesAsync(); }
            TempData["Success"] = "Department deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateHodList()
        {
            var hods = await _uow.Users.FindAsync(u => u.Role.RoleName == "HOD" && u.IsActive);
            ViewBag.HodList = new SelectList(hods, "UserId", "FullName");
        }
    }
}
