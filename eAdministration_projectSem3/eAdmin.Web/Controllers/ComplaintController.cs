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
        private readonly INotificationService _notify;

        public ComplaintController(
            IComplaintService complaintService,
            IUnitOfWork uow,
            INotificationService notify)
        {
            _complaintService = complaintService;
            _uow = uow;
            _notify = notify;
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
                EquipmentId = vm.EquipmentId,
                ComplaintTypeId = vm.ComplaintTypeId,
                Priority = vm.Priority,
                ReportedBy = userId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _complaintService.CreateAndAutoAssignAsync(complaint);

            TempData["Success"] = "Complaint submitted successfully. A technician will be assigned shortly.";
            return RedirectToAction(nameof(Index));
        }

        // ── GET: /Complaint/GetEquipmentByLab?labId=1  (AJAX) ─────────────
        [HttpGet]
        public async Task<IActionResult> GetEquipmentByLab(int labId)
        {
            if (labId <= 0) return Json(Array.Empty<object>());

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
        public async Task<IActionResult> UpdateStatus(int id, string status, string? resolutionNote)
        {
            var role = User.FindFirst("Role")?.Value ?? "";
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // ── Only TechStaff can update status ──────────────────────────
            if (role != "TechStaff")
            {
                TempData["Error"] = "Only TechStaff can update complaint status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrEmpty(status))
            {
                TempData["Error"] = "Please select a status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // TechStaff cannot set Pending or Assigned — system-managed
            if (status == "Pending" || status == "Assigned")
            {
                TempData["Error"] = "Cannot set status to 'Pending' or 'Assigned'. " +
                                    "These are system-managed statuses.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var complaint = await _uow.Complaints.GetByIdAsync(id);
            if (complaint == null)
            {
                TempData["Error"] = "Complaint not found.";
                return RedirectToAction(nameof(Index));
            }

            // Closed complaint cannot be updated
            if (complaint.Status == "Closed")
            {
                TempData["Error"] = "This complaint is already closed and cannot be updated.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var oldStatus = complaint.Status;
            complaint.Status = status;

            if (status == "Resolved" || status == "Closed")
                complaint.ResolvedAt = DateTime.UtcNow;

            // Append technician note to Description with timestamp
            if (!string.IsNullOrWhiteSpace(resolutionNote))
            {
                var techName = User.Identity?.Name ?? "Technician";
                var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                var entry = $"\n\n─── [{timestamp}] {techName} → {status} ───\n{resolutionNote.Trim()}";
                complaint.Description = (complaint.Description ?? "") + entry;
            }

            _uow.Complaints.Update(complaint);

            // ── Auto-update equipment condition based on new status ────────
            if (complaint.EquipmentId.HasValue)
            {
                var equipment = await _uow.Equipments.GetByIdAsync(complaint.EquipmentId.Value);
                if (equipment != null)
                {
                    equipment.Condition = status switch
                    {
                        "InProgress" => "Poor",
                        "Resolved" => "Good",
                        "Closed" => complaint.ResolvedAt.HasValue ? "Fair" : "OutOfService",
                        _ => equipment.Condition
                    };
                    _uow.Equipments.Update(equipment);
                }
            }

            // Audit log
            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                UserId = userId,
                Action = "UpdateComplaintStatus",
                EntityType = "Complaint",
                EntityId = complaint.ComplaintId,
                Details = $"Status: {oldStatus} → {status}" +
                             (!string.IsNullOrWhiteSpace(resolutionNote)
                                 ? $" | Note: {resolutionNote.Trim()}"
                                 : ""),
                CreatedAt = DateTime.UtcNow
            });

            await _uow.SaveChangesAsync();

            // Notify reporter when Resolved or Closed
            if (status == "Resolved" || status == "Closed")
            {
                var noteMsg = !string.IsNullOrWhiteSpace(resolutionNote)
                    ? $" Technician note: {resolutionNote.Trim()}"
                    : string.Empty;

                await _notify.SendAsync(
                    complaint.ReportedBy,
                    "InApp",
                    $"Complaint #{complaint.ComplaintId} {status}",
                    $"Your complaint \"{complaint.Title}\" has been marked as {status}.{noteMsg}",
                    "Complaint",
                    complaint.ComplaintId);
            }

            TempData["Success"] = $"Status updated to '{status}'." +
                                  (!string.IsNullOrWhiteSpace(resolutionNote) ? " Note saved." : "");
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