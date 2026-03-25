using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    [AuthorizeRoles("Admin")]
    public class ComplaintTypeController : Controller
    {
        private readonly IUnitOfWork _uow;
        public ComplaintTypeController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var types = await _uow.ComplaintTypes.GetAllAsync();
            var complaints = await _uow.Complaints.GetAllAsync();

            var vm = types.Select(t => new ComplaintTypeViewModel
            {
                ComplaintTypeId = t.ComplaintTypeId,
                TypeName = t.TypeName,
                Description = t.Description,
                ComplaintCount = complaints.Count(c => c.ComplaintTypeId == t.ComplaintTypeId)
            }).ToList();

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create() => View(new ComplaintTypeViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplaintTypeViewModel vm)
        {
            // Trim input
            vm.TypeName = vm.TypeName?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();

            // Manual validation with error messages shown in view
            if (string.IsNullOrEmpty(vm.TypeName))
            {
                ModelState.AddModelError("TypeName", "Type name is required.");
                return View(vm);
            }

            if (vm.TypeName.Length < 2)
            {
                ModelState.AddModelError("TypeName", "Type name must be at least 2 characters.");
                return View(vm);
            }

            // Duplicate check — case-insensitive
            var existing = await _uow.ComplaintTypes.FindAsync(
                t => t.TypeName.ToLower() == vm.TypeName.ToLower());

            if (existing.Any())
            {
                ModelState.AddModelError("TypeName",
                    $"Complaint type '{vm.TypeName}' already exists. Please use a different name.");
                return View(vm);
            }

            await _uow.ComplaintTypes.AddAsync(new ComplaintType
            {
                TypeName = vm.TypeName,
                Description = vm.Description
            });
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Complaint type '{vm.TypeName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var t = await _uow.ComplaintTypes.GetByIdAsync(id);
            if (t == null) return NotFound();
            return View(new ComplaintTypeViewModel
            {
                ComplaintTypeId = t.ComplaintTypeId,
                TypeName = t.TypeName,
                Description = t.Description
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ComplaintTypeViewModel vm)
        {
            vm.TypeName = vm.TypeName?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();

            if (string.IsNullOrEmpty(vm.TypeName))
            {
                ModelState.AddModelError("TypeName", "Type name is required.");
                return View(vm);
            }

            // Duplicate check — exclude self
            var existing = await _uow.ComplaintTypes.FindAsync(
                t => t.TypeName.ToLower() == vm.TypeName.ToLower() &&
                     t.ComplaintTypeId != vm.ComplaintTypeId);

            if (existing.Any())
            {
                ModelState.AddModelError("TypeName",
                    $"Complaint type '{vm.TypeName}' already exists.");
                return View(vm);
            }

            var t = await _uow.ComplaintTypes.GetByIdAsync(vm.ComplaintTypeId);
            if (t == null) return NotFound();

            t.TypeName = vm.TypeName;
            t.Description = vm.Description;
            _uow.ComplaintTypes.Update(t);
            await _uow.SaveChangesAsync();

            TempData["Success"] = "Complaint type updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Referential integrity check
            var complaints = await _uow.Complaints.FindAsync(c => c.ComplaintTypeId == id);
            if (complaints.Any())
            {
                TempData["Error"] = $"Cannot delete — this type is used by {complaints.Count()} complaint(s). " +
                                     "Please reassign or resolve those complaints first.";
                return RedirectToAction(nameof(Index));
            }

            var t = await _uow.ComplaintTypes.GetByIdAsync(id);
            if (t != null)
            {
                _uow.ComplaintTypes.Remove(t);
                await _uow.SaveChangesAsync();
                TempData["Success"] = $"Complaint type '{t.TypeName}' deleted.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}