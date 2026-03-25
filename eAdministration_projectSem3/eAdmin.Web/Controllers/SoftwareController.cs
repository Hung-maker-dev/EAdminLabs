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

        public SoftwareController(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<IActionResult> Index()
        {
            var all = await _uow.Softwares.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();

            var vm = all.Select(s => new SoftwareViewModel
            {
                SoftwareId = s.SoftwareId,
                SoftwareName = s.SoftwareName,
                Version = s.Version,
                LicenseKey = s.LicenseKey,
                LicenseExpiry = s.LicenseExpiry,
                InstallGuideUrl = s.InstallGuideUrl,
                LabId = s.LabId,
                LabName = labs.FirstOrDefault(l => l.LabId == s.LabId)?.LabName ?? "All Labs",
                IsNotificationSent = s.IsNotificationSent
            }).OrderBy(s => s.SoftwareName).ToList();

            return View(vm);
        }

        // ================= CREATE =================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateLabs();
            return View(new SoftwareViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(SoftwareViewModel vm)
        {
            Validate(vm);

            if (ModelState.IsValid)
            {
                var exist = (await _uow.Softwares.GetAllAsync())
                    .Any(s => string.Equals(s.SoftwareName, vm.SoftwareName, StringComparison.OrdinalIgnoreCase));

                if (exist)
                    ModelState.AddModelError("SoftwareName", "Software name already exists");
            }

            if (!ModelState.IsValid)
            {
                await PopulateLabs();
                return View(vm);
            }

            await _uow.Softwares.AddAsync(new Software
            {
                SoftwareName = vm.SoftwareName,
                Version = vm.Version,
                LicenseKey = vm.LicenseKey,
                LicenseExpiry = vm.LicenseExpiry,
                InstallGuideUrl = vm.InstallGuideUrl,
                LabId = vm.LabId,
                IsNotificationSent = false
            });

            await _uow.SaveChangesAsync();

            TempData["Success"] = "Software added successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _uow.Softwares.GetByIdAsync(id);
            if (s == null) return NotFound();

            await PopulateLabs();

            return View(new SoftwareViewModel
            {
                SoftwareId = s.SoftwareId,
                SoftwareName = s.SoftwareName,
                Version = s.Version,
                LicenseKey = s.LicenseKey,
                LicenseExpiry = s.LicenseExpiry,
                InstallGuideUrl = s.InstallGuideUrl,
                LabId = s.LabId,
                IsNotificationSent = s.IsNotificationSent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(SoftwareViewModel vm)
        {
            Validate(vm);

            if (ModelState.IsValid)
            {
                var exist = (await _uow.Softwares.GetAllAsync())
                    .Any(s => s.SoftwareId != vm.SoftwareId &&
                              string.Equals(s.SoftwareName, vm.SoftwareName, StringComparison.OrdinalIgnoreCase));

                if (exist)
                    ModelState.AddModelError("SoftwareName", "Software name already exists");
            }

            if (!ModelState.IsValid)
            {
                await PopulateLabs();
                return View(vm);
            }

            var s = await _uow.Softwares.GetByIdAsync(vm.SoftwareId);
            if (s == null) return NotFound();

            s.SoftwareName = vm.SoftwareName;
            s.Version = vm.Version;
            s.LicenseKey = vm.LicenseKey;
            s.LicenseExpiry = vm.LicenseExpiry;
            s.InstallGuideUrl = vm.InstallGuideUrl;
            s.LabId = vm.LabId;

            _uow.Softwares.Update(s);
            await _uow.SaveChangesAsync();

            TempData["Success"] = "Software updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _uow.Softwares.GetByIdAsync(id);
            if (s != null)
            {
                _uow.Softwares.Remove(s);
                await _uow.SaveChangesAsync();
            }

            TempData["Success"] = "Software deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ================= VALIDATE =================
        private void Validate(SoftwareViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.SoftwareName))
                ModelState.AddModelError("SoftwareName", "Software Name is required");

            if (string.IsNullOrWhiteSpace(vm.Version))
                ModelState.AddModelError("Version", "Version is required");

            if (string.IsNullOrWhiteSpace(vm.LicenseKey))
                ModelState.AddModelError("LicenseKey", "License Key is required");

            if (string.IsNullOrWhiteSpace(vm.InstallGuideUrl))
                ModelState.AddModelError("InstallGuideUrl", "Install Guide URL is required");

            if (vm.SoftwareName?.Length < 2)
                ModelState.AddModelError("SoftwareName", "Must be at least 2 characters");
        }

        private async Task PopulateLabs()
        {
            var labs = await _uow.Labs.FindAsync(l => l.IsActive);
            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
        }
    }
}