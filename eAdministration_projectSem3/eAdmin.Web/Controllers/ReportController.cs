using System;
using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    [AuthorizeRoles("Admin", "HOD", "Instructor")]
    public class ReportController : Controller
    {
        private readonly IUnitOfWork _uow;
        public ReportController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Equipment(int? labId, string? condition, string? keyword)
        {
            var all = await _uow.Equipments.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();
            var types = await _uow.EquipmentTypes.GetAllAsync();

            if (labId.HasValue) all = all.Where(e => e.LabId == labId);
            if (!string.IsNullOrEmpty(condition)) all = all.Where(e => e.Condition == condition);
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();

                all = all.Where(e =>
                    (e.AssetCode != null && e.AssetCode.ToLower().Contains(keyword)) ||
                    (types.FirstOrDefault(t => t.EquipmentTypeId == e.EquipmentTypeId)
                        .TypeName.ToLower().Contains(keyword))
                );
            }

            ViewBag.Keyword = keyword;

            var vm = all.Select(e => new EquipmentViewModel
            {
                EquipmentId = e.EquipmentId,
                AssetCode = e.AssetCode,
                Model = e.Model,
                SerialNumber = e.SerialNumber,
                PurchaseDate = e.PurchaseDate,
                WarrantyExpiry = e.WarrantyExpiry,
                Condition = e.Condition,
                Notes = e.Notes,
                LabId = e.LabId,
                EquipmentTypeId = e.EquipmentTypeId,
                LabName = labs.FirstOrDefault(l => l.LabId == e.LabId)?.LabName ?? "",
                TypeName = types.FirstOrDefault(t => t.EquipmentTypeId == e.EquipmentTypeId)?.TypeName ?? ""
            }).ToList();

            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.FilterLab = labId; ViewBag.FilterCondition = condition;
            ViewBag.Summary = new
            {
                Good = vm.Count(e => e.Condition == "Good"),
                Fair = vm.Count(e => e.Condition == "Fair"),
                Poor = vm.Count(e => e.Condition == "Poor"),
                OutOfService = vm.Count(e => e.Condition == "OutOfService"),
                WarrantyExpired = vm.Count(e => e.WarrantyExpired),
                ExpiringSoon = vm.Count(e => e.WarrantyExpiringSoon)
            };
            return View(vm);
        }

        public async Task<IActionResult> Complaints(DateTime? from, DateTime? to, string? status, int? labId, string? keyword)
        {
            var all = await _uow.Complaints.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var types = await _uow.ComplaintTypes.GetAllAsync();

            if (from.HasValue) all = all.Where(c => c.CreatedAt >= from.Value);
            if (to.HasValue) all = all.Where(c => c.CreatedAt <= to.Value.AddDays(1));
            if (!string.IsNullOrEmpty(status)) all = all.Where(c => c.Status == status);
            if (labId.HasValue) all = all.Where(c => c.LabId == labId);
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();

                all = all.Where(c =>
                    (c.Title != null && c.Title.ToLower().Contains(keyword)) ||
                    (c.Status != null && c.Status.ToLower().Contains(keyword))
                );
            }

            var vm = all.Select(c => new ComplaintListViewModel
            {
                ComplaintId = c.ComplaintId,
                Title = c.Title,
                Status = c.Status,
                Priority = c.Priority,
                LabName = labs.FirstOrDefault(l => l.LabId == c.LabId)?.LabName ?? "",
                TypeName = types.FirstOrDefault(t => t.ComplaintTypeId == c.ComplaintTypeId)?.TypeName ?? "",
                ReporterName = users.FirstOrDefault(u => u.UserId == c.ReportedBy)?.FullName ?? "",
                AssigneeName = c.AssignedTo.HasValue ? users.FirstOrDefault(u => u.UserId == c.AssignedTo)?.FullName : null,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt
            }).ToList();

            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.FilterStatus = status; ViewBag.FilterLab = labId;
            ViewBag.Keyword = keyword;
            ViewBag.Summary = new
            {
                Total = vm.Count,
                Pending = vm.Count(c => c.Status == "Pending"),
                InProgress = vm.Count(c => c.Status == "InProgress"),
                Resolved = vm.Count(c => c.Status == "Resolved"),
                Closed = vm.Count(c => c.Status == "Closed")
            };
            return View(vm);
        }

        public async Task<IActionResult> SoftwareExpiry(string? keyword, int? labId)
        {
            var all = await _uow.Softwares.GetAllAsync();
            var labs = await _uow.Labs.GetAllAsync();

            var threshold = DateTime.Today.AddDays(60);

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();

                all = all.Where(s =>
                    (s.SoftwareName != null && s.SoftwareName.ToLower().Contains(keyword)) ||
                    (s.Version != null && s.Version.ToLower().Contains(keyword))
                );
            }

            if (labId.HasValue)
            {
                all = all.Where(s => s.LabId == labId);
            }

            var expiring = all
                .Where(s => s.LicenseExpiry.HasValue && s.LicenseExpiry.Value.Date <= threshold)
                .OrderBy(s => s.LicenseExpiry);

            var vm = expiring.Select(s => new SoftwareViewModel
            {
                SoftwareId = s.SoftwareId,
                SoftwareName = s.SoftwareName,
                Version = s.Version,
                LicenseExpiry = s.LicenseExpiry,
                LabId = s.LabId,
                IsNotificationSent = s.IsNotificationSent,
                LabName = labs.FirstOrDefault(l => l.LabId == s.LabId)?.LabName ?? "All Labs"
            }).ToList();

            ViewBag.Keyword = keyword;
            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.FilterLab = labId;

            return View(vm);
        }

        public async Task<IActionResult> Overview(int? labId, DateTime? from, DateTime? to)
        {
            var labs = await _uow.Labs.GetAllAsync();
            var equips = await _uow.Equipments.GetAllAsync();
            var complaints = await _uow.Complaints.GetAllAsync();
            var softwares = await _uow.Softwares.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var schedules = await _uow.Schedules.GetAllAsync();

            // FILTER
            if (labId.HasValue)
            {
                equips = equips.Where(e => e.LabId == labId);
                complaints = complaints.Where(c => c.LabId == labId);
                softwares = softwares.Where(s => s.LabId == labId);
                schedules = schedules.Where(s => s.LabId == labId);
            }

            if (from.HasValue)
            {
                complaints = complaints.Where(c => c.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                complaints = complaints.Where(c => c.CreatedAt <= to.Value.AddDays(1));
            }

            // VIEWBAG FILTER
            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.FilterLab = labId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");

            // DATA
            ViewBag.TotalLabs = labs.Count();
            ViewBag.ActiveLabs = labs.Count(l => l.IsActive);
            ViewBag.TotalEquipment = equips.Count();
            ViewBag.EquipmentGood = equips.Count(e => e.Condition == "Good");
            ViewBag.EquipmentPoor = equips.Count(e => e.Condition == "Poor" || e.Condition == "OutOfService");
            ViewBag.TotalComplaints = complaints.Count();
            ViewBag.OpenComplaints = complaints.Count(c => c.Status != "Closed" && c.Status != "Resolved");
            ViewBag.TotalUsers = users.Count();
            ViewBag.ActiveUsers = users.Count(u => u.IsActive);
            ViewBag.ExpiringSW = softwares.Count(s => s.LicenseExpiry.HasValue && s.LicenseExpiry.Value.Date <= DateTime.Today.AddDays(30));
            ViewBag.TotalSchedules = schedules.Count(s => s.IsActive);

            return View();
        }
    }
}
