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
    public class SoftwareController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;
        public SoftwareController(IUnitOfWork uow, INotificationService notify) { _uow = uow; _notify = notify; }

        public async Task<IActionResult> Index()
        {
            var all = await _uow.Softwares.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();
            var vm = all.Select(s => new SoftwareViewModel
            {
                SoftwareId = s.SoftwareId, SoftwareName = s.SoftwareName, Version = s.Version,
                LicenseKey = s.LicenseKey, LicenseExpiry = s.LicenseExpiry,
                InstallGuideUrl = s.InstallGuideUrl, LabId = s.LabId,
                LabName = labs.FirstOrDefault(l => l.LabId == s.LabId)?.LabName ?? "All Labs",
                IsNotificationSent = s.IsNotificationSent
            }).OrderBy(s => s.LicenseExpiry).ToList();
            return View(vm);
        }

        [HttpGet][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateLabs();
            return View(new SoftwareViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(SoftwareViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateLabs(); return View(vm); }
            await _uow.Softwares.AddAsync(new Software
            {
                SoftwareName = vm.SoftwareName, Version = vm.Version, LicenseKey = vm.LicenseKey,
                LicenseExpiry = vm.LicenseExpiry, InstallGuideUrl = vm.InstallGuideUrl,
                LabId = vm.LabId, IsNotificationSent = false
            });
            await WriteAuditAsync("CreateSoftware", "Software", 0, vm.SoftwareName);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Software added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _uow.Softwares.GetByIdAsync(id);
            if (s == null) return NotFound();
            await PopulateLabs();
            return View(new SoftwareViewModel
            {
                SoftwareId = s.SoftwareId, SoftwareName = s.SoftwareName, Version = s.Version,
                LicenseKey = s.LicenseKey, LicenseExpiry = s.LicenseExpiry,
                InstallGuideUrl = s.InstallGuideUrl, LabId = s.LabId, IsNotificationSent = s.IsNotificationSent
            });
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(SoftwareViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateLabs(); return View(vm); }
            var s = await _uow.Softwares.GetByIdAsync(vm.SoftwareId);
            if (s == null) return NotFound();
            bool dateChanged = s.LicenseExpiry != vm.LicenseExpiry;
            s.SoftwareName = vm.SoftwareName; s.Version = vm.Version; s.LicenseKey = vm.LicenseKey;
            s.LicenseExpiry = vm.LicenseExpiry; s.InstallGuideUrl = vm.InstallGuideUrl; s.LabId = vm.LabId;
            if (dateChanged) s.IsNotificationSent = false; // reset so background service re-alerts
            _uow.Softwares.Update(s);
            await WriteAuditAsync("EditSoftware", "Software", s.SoftwareId, s.SoftwareName);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Software updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken][AuthorizeRoles("Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _uow.Softwares.GetByIdAsync(id);
            if (s != null) { _uow.Softwares.Remove(s); await _uow.SaveChangesAsync(); }
            TempData["Success"] = "Software deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLabs()
        {
            var labs = await _uow.Labs.FindAsync(l => l.IsActive);
            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
        }

        private async Task WriteAuditAsync(string action, string entityType, int entityId, string details)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _uow.AuditLogs.AddAsync(new Domain.Entities.AuditLog { UserId = uid, Action = action, EntityType = entityType, EntityId = entityId, Details = details });
        }
    }
}
