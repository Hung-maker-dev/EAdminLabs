using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    public class ComplaintController : Controller
    {
        private readonly IComplaintService _complaintService;
        private readonly IUnitOfWork _uow;

        public ComplaintController(IComplaintService complaintService, IUnitOfWork uow)
        {
            _complaintService = complaintService;
            _uow = uow;
        }

        // ── GET: /Complaint ────────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst("Role")?.Value ?? "";
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var deptClaim = User.FindFirst("DepartmentId")?.Value;
            var deptId = int.TryParse(deptClaim, out var d) ? (int?)d : null;

            var complaints = role switch
            {
                "Admin" => await _uow.Complaints.GetAllAsync(),
                "HOD" => deptId.HasValue
                                    ? await _complaintService.GetComplaintsByDepartmentAsync(deptId.Value)
                                    : Enumerable.Empty<Complaint>(),
                "TechStaff" => await _complaintService.GetComplaintsByUserAsync(userId),
                "Instructor" => await _complaintService.GetComplaintsByUserAsync(userId),
                _ => Enumerable.Empty<Complaint>()
            };

            var labs = await _uow.Labs.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var types = await _uow.ComplaintTypes.GetAllAsync();

            var vm = complaints.Select(c => new ComplaintListViewModel
            {
                ComplaintId = c.ComplaintId,
                Title = c.Title,
                Status = c.Status,
                Priority = c.Priority,
                LabName = labs.FirstOrDefault(l => l.LabId == c.LabId)?.LabName ?? "",
                TypeName = types.FirstOrDefault(t => t.ComplaintTypeId == c.ComplaintTypeId)?.TypeName ?? "",
                ReporterName = users.FirstOrDefault(u => u.UserId == c.ReportedBy)?.FullName ?? "",
                AssigneeName = c.AssignedTo.HasValue
                                ? users.FirstOrDefault(u => u.UserId == c.AssignedTo)?.FullName
                                : null,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt
            }).OrderByDescending(c => c.CreatedAt).ToList();

            return View(vm);
        }

        // ── GET: /Complaint/Create ─────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View(new ComplaintCreateViewModel());
        }

        // ── POST: /Complaint/Create ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplaintCreateViewModel vm)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(vm.Title))
                ModelState.AddModelError("Title", "Title is required.");
            else if (vm.Title.Trim().Length < 5)
                ModelState.AddModelError("Title", "Title must be at least 5 characters.");

            if (vm.LabId == 0)
                ModelState.AddModelError("LabId", "Please select a lab.");

            if (vm.ComplaintTypeId == 0)
                ModelState.AddModelError("ComplaintTypeId", "Please select a complaint type.");

            if (string.IsNullOrWhiteSpace(vm.Description))
                ModelState.AddModelError("Description", "Description is required.");
            else if (vm.Description.Trim().Length < 10)
                ModelState.AddModelError("Description", "Description must be at least 10 characters.");

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(vm);
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var complaint = new Complaint
            {
                Title = vm.Title.Trim(),
                Description = vm.Description.Trim(),
                LabId = vm.LabId,
                EquipmentId = vm.EquipmentId,   // nullable — from AJAX dropdown
                ComplaintTypeId = vm.ComplaintTypeId,
                Priority = vm.Priority,
                ReportedBy = userId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _complaintService.CreateAndAutoAssignAsync(complaint);

            TempData["Success"] = "Complaint submitted. A technician will be assigned shortly.";
            return RedirectToAction(nameof(Index));
        }

        // ── GET: /Complaint/GetEquipmentByLab?labId=1  (AJAX endpoint) ────
        [HttpGet]
        public async Task<IActionResult> GetEquipmentByLab(int labId)
        {
            if (labId <= 0)
                return Json(Array.Empty<object>());

            var equips = await _uow.Equipments.FindAsync(
                e => e.LabId == labId && e.Condition != "OutOfService");

            var types = await _uow.EquipmentTypes.GetAllAsync();

            var result = equips.Select(e => new
            {
                equipmentId = e.EquipmentId,
                assetCode = e.AssetCode,
                model = e.Model ?? "",
                condition = e.Condition,
                typeName = types.FirstOrDefault(t => t.EquipmentTypeId == e.EquipmentTypeId)?.TypeName ?? "Unknown"
            }).OrderBy(e => e.typeName).ThenBy(e => e.assetCode).ToList();

            return Json(result);
        }

        // ── GET: /Complaint/Details/5 ──────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var complaint = await _uow.Complaints.GetByIdAsync(id);
            if (complaint == null) return NotFound();

            var labs = await _uow.Labs.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var types = await _uow.ComplaintTypes.GetAllAsync();
            var equips = await _uow.Equipments.GetAllAsync();

            complaint.Lab = labs.FirstOrDefault(l => l.LabId == complaint.LabId) ?? new Lab();
            complaint.Reporter = users.FirstOrDefault(u => u.UserId == complaint.ReportedBy) ?? new User();
            complaint.Assignee = complaint.AssignedTo.HasValue
                                      ? users.FirstOrDefault(u => u.UserId == complaint.AssignedTo)
                                      : null;
            complaint.ComplaintType = types.FirstOrDefault(t => t.ComplaintTypeId == complaint.ComplaintTypeId)
                                      ?? new ComplaintType();
            complaint.Equipment = complaint.EquipmentId.HasValue
                                      ? equips.FirstOrDefault(e => e.EquipmentId == complaint.EquipmentId)
                                      : null;
            return View(complaint);
        }

        // ── POST: /Complaint/UpdateStatus ──────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                TempData["Error"] = "Please select a status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var ok = await _complaintService.UpdateStatusAsync(id, status, userId);

            TempData[ok ? "Success" : "Error"] = ok
                ? $"Status updated to '{status}' successfully."
                : "Complaint not found.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private async Task PopulateDropdownsAsync()
        {
            var labs = await _uow.Labs.FindAsync(l => l.IsActive);
            var types = await _uow.ComplaintTypes.GetAllAsync();
            ViewBag.Labs = new SelectList(labs, "LabId", "LabName");
            ViewBag.Types = new SelectList(types, "ComplaintTypeId", "TypeName");
        }
    }
}