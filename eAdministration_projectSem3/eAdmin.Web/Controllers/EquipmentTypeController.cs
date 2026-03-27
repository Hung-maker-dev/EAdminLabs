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
    [AuthorizeRoles("Admin", "Instructor", "TechStaff")]
    public class EquipmentTypeController : Controller
    {
        private readonly IUnitOfWork _uow;
        public EquipmentTypeController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var types = await _uow.EquipmentTypes.GetAllAsync();
            var equips = await _uow.Equipments.GetAllAsync();

            var vm = types.Select(t => new EquipmentTypeViewModel
            {
                EquipmentTypeId = t.EquipmentTypeId,
                TypeName = t.TypeName,
                Description = t.Description,
                EquipmentCount = equips.Count(e => e.EquipmentTypeId == t.EquipmentTypeId)
            }).ToList();

            return View(vm);
        }

      
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public IActionResult Create() => View(new EquipmentTypeViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(EquipmentTypeViewModel vm)
        {
           
            vm.TypeName = vm.TypeName?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();

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

           
            var inputName = vm.TypeName.ToLower();

            var existing = await _uow.EquipmentTypes
                .FindAsync(t => t.TypeName.ToLower() == inputName);

            if (existing.Any())
            {
                ModelState.AddModelError("TypeName",
                    $"Equipment type '{vm.TypeName}' already exists.");
                return View(vm);
            }

           
            await _uow.EquipmentTypes.AddAsync(new EquipmentType
            {
                TypeName = vm.TypeName,
                Description = vm.Description
            });

            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Equipment type '{vm.TypeName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var t = await _uow.EquipmentTypes.GetByIdAsync(id);
            if (t == null) return NotFound();

            return View(new EquipmentTypeViewModel
            {
                EquipmentTypeId = t.EquipmentTypeId,
                TypeName = t.TypeName,
                Description = t.Description
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(EquipmentTypeViewModel vm)
        {
          
            vm.TypeName = vm.TypeName?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();

           
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

           
            var inputName = vm.TypeName.ToLower();

            var existing = await _uow.EquipmentTypes
                .FindAsync(t => t.TypeName.ToLower() == inputName
                             && t.EquipmentTypeId != vm.EquipmentTypeId);

            if (existing.Any())
            {
                ModelState.AddModelError("TypeName",
                    $"Equipment type '{vm.TypeName}' already exists.");
                return View(vm);
            }

            var t = await _uow.EquipmentTypes.GetByIdAsync(vm.EquipmentTypeId);
            if (t == null) return NotFound();

            t.TypeName = vm.TypeName;
            t.Description = vm.Description;

            _uow.EquipmentTypes.Update(t);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Equipment type '{vm.TypeName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Delete(int id)
        {
        
            var equips = await _uow.Equipments
                .FindAsync(e => e.EquipmentTypeId == id);

            if (equips.Any())
            {
                TempData["Error"] =
                    $"Cannot delete — this type is used by {equips.Count()} equipment(s).";
                return RedirectToAction(nameof(Index));
            }

            var t = await _uow.EquipmentTypes.GetByIdAsync(id);
            if (t != null)
            {
                _uow.EquipmentTypes.Remove(t);
                await _uow.SaveChangesAsync();

                TempData["Success"] =
                    $"Equipment type '{t.TypeName}' deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}