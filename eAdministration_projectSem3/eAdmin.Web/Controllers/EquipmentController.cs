using System;
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
    [Authorize]
    public class EquipmentController : Controller
    {
        private readonly IUnitOfWork _uow;
        public EquipmentController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index(int? labId, string? condition)
        {
            var all = await _uow.Equipments.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();
            var types = await _uow.EquipmentTypes.GetAllAsync();

            if (labId.HasValue) all = all.Where(e => e.LabId == labId);
            if (!string.IsNullOrEmpty(condition)) all = all.Where(e => e.Condition == condition);

            var vm = all.Select(e => new EquipmentViewModel
            {
                EquipmentId = e.EquipmentId, AssetCode = e.AssetCode, LabId = e.LabId,
                EquipmentTypeId = e.EquipmentTypeId, Model = e.Model, SerialNumber = e.SerialNumber,
                PurchaseDate = e.PurchaseDate, WarrantyExpiry = e.WarrantyExpiry,
                Condition = e.Condition, Notes = e.Notes,
                LabName = labs.FirstOrDefault(l => l.LabId == e.LabId)?.LabName ?? "",
                TypeName = types.FirstOrDefault(t => t.EquipmentTypeId == e.EquipmentTypeId)?.TypeName ?? ""
            }).ToList();

            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.FilterLab = labId; ViewBag.FilterCondition = condition;
            return View(vm);
        }

        [HttpGet][AuthorizeRoles("Admin","Instructor")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new EquipmentViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin","Instructor")]
        public async Task<IActionResult> Create(EquipmentViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }
            var exists = await _uow.Equipments.FindAsync(e => e.AssetCode == vm.AssetCode);
            if (exists.Any())
            {
                ModelState.AddModelError("AssetCode", "Asset code already exists.");
                await PopulateDropdowns(); return View(vm);
            }
            await _uow.Equipments.AddAsync(new Equipment
            {
                AssetCode = vm.AssetCode, LabId = vm.LabId, EquipmentTypeId = vm.EquipmentTypeId,
                Model = vm.Model, SerialNumber = vm.SerialNumber, PurchaseDate = vm.PurchaseDate,
                WarrantyExpiry = vm.WarrantyExpiry, Condition = vm.Condition, Notes = vm.Notes
            });
            await WriteAuditAsync("CreateEquipment", "Equipment", 0, vm.AssetCode);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Equipment added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet][AuthorizeRoles("Admin","Instructor","TechStaff")]
        public async Task<IActionResult> Edit(int id)
        {
            var e = await _uow.Equipments.GetByIdAsync(id);
            if (e == null) return NotFound();
            await PopulateDropdowns();
            return View(new EquipmentViewModel
            {
                EquipmentId = e.EquipmentId, AssetCode = e.AssetCode, LabId = e.LabId,
                EquipmentTypeId = e.EquipmentTypeId, Model = e.Model, SerialNumber = e.SerialNumber,
                PurchaseDate = e.PurchaseDate, WarrantyExpiry = e.WarrantyExpiry,
                Condition = e.Condition, Notes = e.Notes
            });
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin","Instructor","TechStaff")]
        public async Task<IActionResult> Edit(EquipmentViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }
            var e = await _uow.Equipments.GetByIdAsync(vm.EquipmentId);
            if (e == null) return NotFound();
            e.LabId = vm.LabId; e.EquipmentTypeId = vm.EquipmentTypeId; e.Model = vm.Model;
            e.SerialNumber = vm.SerialNumber; e.PurchaseDate = vm.PurchaseDate;
            e.WarrantyExpiry = vm.WarrantyExpiry; e.Condition = vm.Condition; e.Notes = vm.Notes;
            _uow.Equipments.Update(e);
            await WriteAuditAsync("EditEquipment", "Equipment", e.EquipmentId, $"Condition={vm.Condition}");
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Equipment updated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Labs = new SelectList(await _uow.Labs.FindAsync(l => l.IsActive), "LabId", "LabName");
            ViewBag.EquipmentTypes = new SelectList(await _uow.EquipmentTypes.GetAllAsync(), "EquipmentTypeId", "TypeName");
        }

        private async Task WriteAuditAsync(string action, string entityType, int entityId, string details)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _uow.AuditLogs.AddAsync(new AuditLog { UserId = uid, Action = action, EntityType = entityType, EntityId = entityId, Details = details });
        }
    }
}
