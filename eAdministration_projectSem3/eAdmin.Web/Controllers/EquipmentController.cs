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

        public async Task<IActionResult> Index(int? labId, string? condition, string? assetCode)
        {
            var all = await _uow.Equipments.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();
            var types = await _uow.EquipmentTypes.GetAllAsync();

            if (labId.HasValue)
                all = all.Where(e => e.LabId == labId);

            if (!string.IsNullOrEmpty(condition))
                all = all.Where(e => e.Condition == condition);

            if (!string.IsNullOrEmpty(assetCode))
                all = all.Where(e => e.AssetCode.ToLower().Contains(assetCode.ToLower()));

            var vm = all.Select(e => new EquipmentViewModel
            {
                EquipmentId = e.EquipmentId,
                AssetCode = e.AssetCode,
                LabId = e.LabId,
                EquipmentTypeId = e.EquipmentTypeId,
                Model = e.Model,
                SerialNumber = e.SerialNumber,
                PurchaseDate = e.PurchaseDate,
                WarrantyExpiry = e.WarrantyExpiry,
                Condition = e.Condition,
                Notes = e.Notes,
                LabName = labs.FirstOrDefault(l => l.LabId == e.LabId)?.LabName ?? "",
                TypeName = types.FirstOrDefault(t => t.EquipmentTypeId == e.EquipmentTypeId)?.TypeName ?? ""
            }).ToList();

            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            return View(vm);
        }

        [HttpGet]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new EquipmentViewModel { Condition = "Good" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Create(EquipmentViewModel vm)
        {
            vm.AssetCode = vm.AssetCode?.Trim() ?? "";
            vm.Model = vm.Model?.Trim();
            vm.SerialNumber = vm.SerialNumber?.Trim();
            vm.Notes = vm.Notes?.Trim();

            if (string.IsNullOrEmpty(vm.Condition))
                vm.Condition = "Good";

            if (string.IsNullOrEmpty(vm.AssetCode))
                ModelState.AddModelError("AssetCode", "Asset code is required.");
            else if (vm.AssetCode.Length < 2)
                ModelState.AddModelError("AssetCode", "Asset code must be at least 2 characters.");
            else if (vm.AssetCode.Length > 50)
                ModelState.AddModelError("AssetCode", "Asset code must not exceed 50 characters.");

            if (vm.LabId == null || vm.LabId <= 0)
                ModelState.AddModelError("LabId", "Please select a lab.");

            if (vm.EquipmentTypeId == null || vm.EquipmentTypeId <= 0)
                ModelState.AddModelError("EquipmentTypeId", "Please select an equipment type.");

            if (vm.PurchaseDate == null)
                ModelState.AddModelError("PurchaseDate", "Purchase date is required.");
            else if (vm.PurchaseDate.Value > DateTime.Today)
                ModelState.AddModelError("PurchaseDate", "Purchase date cannot be in the future.");
            else if (vm.PurchaseDate.Value.Year < 1990)
                ModelState.AddModelError("PurchaseDate", "Purchase date too old.");

            if (vm.WarrantyExpiry == null)
                ModelState.AddModelError("WarrantyExpiry", "Warranty expiry date is required.");
            else if (vm.PurchaseDate.HasValue && vm.WarrantyExpiry.Value <= vm.PurchaseDate.Value)
                ModelState.AddModelError("WarrantyExpiry", "Warranty must be after purchase date.");
            else if (vm.PurchaseDate.HasValue && (vm.WarrantyExpiry.Value - vm.PurchaseDate.Value).TotalDays < 30)
                ModelState.AddModelError("WarrantyExpiry", "Warranty must be at least 30 days after purchase date.");

            var validConditions = new[] { "Good", "Fair", "Poor" };
            if (!validConditions.Contains(vm.Condition))
                ModelState.AddModelError("Condition", "Invalid condition selected.");

            if (!string.IsNullOrEmpty(vm.AssetCode) &&
                vm.AssetCode.Length >= 2 && vm.AssetCode.Length <= 50)
            {
                var assetLower = vm.AssetCode.ToLower();
                var exists = await _uow.Equipments
                    .FindAsync(e => e.AssetCode.ToLower() == assetLower);

                if (exists.Any())
                    ModelState.AddModelError("AssetCode",
                        $"Asset code '{vm.AssetCode}' already exists.");
            }

            if (!string.IsNullOrEmpty(vm.SerialNumber))
            {
                var snLower = vm.SerialNumber.ToLower();
                var snExists = await _uow.Equipments.FindAsync(e =>
                    !string.IsNullOrEmpty(e.SerialNumber) &&
                    e.SerialNumber.ToLower() == snLower);

                if (snExists.Any())
                    ModelState.AddModelError("SerialNumber",
                        $"Serial number '{vm.SerialNumber}' already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(vm);
            }

            var entity = new Equipment
            {
                AssetCode = vm.AssetCode,
                LabId = vm.LabId!.Value,
                EquipmentTypeId = vm.EquipmentTypeId!.Value,
                Model = vm.Model,
                SerialNumber = vm.SerialNumber,
                PurchaseDate = vm.PurchaseDate!.Value,
                WarrantyExpiry = vm.WarrantyExpiry!.Value,
                Condition = vm.Condition,
                Notes = vm.Notes
            };

            await _uow.Equipments.AddAsync(entity);
            await _uow.SaveChangesAsync();

            await WriteAuditAsync("CreateEquipment", "Equipment", entity.EquipmentId, vm.AssetCode);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Equipment '{vm.AssetCode}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AuthorizeRoles("Admin", "Instructor", "TechStaff")]
        public async Task<IActionResult> Edit(int id)
        {
            var e = await _uow.Equipments.GetByIdAsync(id);
            if (e == null) return NotFound();

            await PopulateDropdowns();

            return View(new EquipmentViewModel
            {
                EquipmentId = e.EquipmentId,
                AssetCode = e.AssetCode,
                LabId = e.LabId,
                EquipmentTypeId = e.EquipmentTypeId,
                Model = e.Model,
                SerialNumber = e.SerialNumber,
                PurchaseDate = e.PurchaseDate,
                WarrantyExpiry = e.WarrantyExpiry,
                Condition = string.IsNullOrEmpty(e.Condition) ? "Good" : e.Condition,
                Notes = e.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor", "TechStaff")]
        public async Task<IActionResult> Edit(EquipmentViewModel vm)
        {
            vm.AssetCode = vm.AssetCode?.Trim() ?? "";
            vm.Model = vm.Model?.Trim();
            vm.SerialNumber = vm.SerialNumber?.Trim();
            vm.Notes = vm.Notes?.Trim();

            var validConditions = new[] { "Good", "Fair", "Poor" };
            if (!validConditions.Contains(vm.Condition))
                ModelState.AddModelError("Condition", "Invalid condition.");

            if (vm.PurchaseDate == null)
                ModelState.AddModelError("PurchaseDate", "Purchase date is required.");
            else if (vm.PurchaseDate.Value > DateTime.Today)
                ModelState.AddModelError("PurchaseDate", "Purchase date cannot be in the future.");
            else if (vm.PurchaseDate.Value.Year < 1990)
                ModelState.AddModelError("PurchaseDate", "Purchase date too old.");

            if (vm.WarrantyExpiry == null)
                ModelState.AddModelError("WarrantyExpiry", "Warranty expiry date is required.");
            else if (vm.PurchaseDate.HasValue && vm.WarrantyExpiry.Value <= vm.PurchaseDate.Value)
                ModelState.AddModelError("WarrantyExpiry", "Warranty must be after purchase date.");
            else if (vm.PurchaseDate.HasValue && (vm.WarrantyExpiry.Value - vm.PurchaseDate.Value).TotalDays < 30)
                ModelState.AddModelError("WarrantyExpiry", "Warranty must be at least 30 days after purchase date.");

            var assetLower = vm.AssetCode.ToLower();
            var exists = await _uow.Equipments.FindAsync(e =>
                e.AssetCode.ToLower() == assetLower &&
                e.EquipmentId != vm.EquipmentId);

            if (exists.Any())
                ModelState.AddModelError("AssetCode",
                    $"Asset code '{vm.AssetCode}' already exists.");

            if (!string.IsNullOrEmpty(vm.SerialNumber))
            {
                var snLower = vm.SerialNumber.ToLower();
                var snExists = await _uow.Equipments.FindAsync(e =>
                    !string.IsNullOrEmpty(e.SerialNumber) &&
                    e.SerialNumber.ToLower() == snLower &&
                    e.EquipmentId != vm.EquipmentId);

                if (snExists.Any())
                    ModelState.AddModelError("SerialNumber",
                        $"Serial number '{vm.SerialNumber}' already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(vm);
            }

            var e = await _uow.Equipments.GetByIdAsync(vm.EquipmentId);
            if (e == null) return NotFound();

            e.AssetCode = vm.AssetCode;
            e.LabId = vm.LabId!.Value;
            e.EquipmentTypeId = vm.EquipmentTypeId!.Value;
            e.Model = vm.Model;
            e.SerialNumber = vm.SerialNumber;
            e.PurchaseDate = vm.PurchaseDate!.Value;
            e.WarrantyExpiry = vm.WarrantyExpiry!.Value;
            e.Condition = vm.Condition;
            e.Notes = vm.Notes;

            _uow.Equipments.Update(e);

            await WriteAuditAsync("EditEquipment", "Equipment", e.EquipmentId, vm.AssetCode);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Equipment '{vm.AssetCode}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Delete(int id)
        {
            var e = await _uow.Equipments.GetByIdAsync(id);
            if (e == null) return NotFound();

            var assetCode = e.AssetCode;
            _uow.Equipments.Remove(e);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Equipment '{assetCode}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Labs = new SelectList(await _uow.Labs.GetAllAsync(), "LabId", "LabName");
            ViewBag.EquipmentTypes = new SelectList(await _uow.EquipmentTypes.GetAllAsync(), "EquipmentTypeId", "TypeName");
        }

        private async Task WriteAuditAsync(string action, string entityType, int entityId, string details)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                UserId = uid,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details
            });
        }
    }
}