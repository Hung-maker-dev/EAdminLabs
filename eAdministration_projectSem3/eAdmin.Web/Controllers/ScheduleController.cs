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
    public class ScheduleController : Controller
    {
        private readonly IScheduleService _scheduleService;
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;

        public ScheduleController(IScheduleService scheduleService, IUnitOfWork uow, INotificationService notify)
        { _scheduleService = scheduleService; _uow = uow; _notify = notify; }

        public async Task<IActionResult> Index()
        {
            var role = User.FindFirst("Role")?.Value ?? "";
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var schedules = role == "Instructor"
                ? await _scheduleService.GetScheduleByInstructorAsync(userId)
                : await _uow.Schedules.GetAllAsync();

            var labs = await _uow.Labs.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var today = DateTime.Today;

            var vm = schedules.Select(s => new ScheduleListItem
            {
                ScheduleId = s.ScheduleId,
                SubjectName = s.SubjectName,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EffectiveFrom = s.EffectiveFrom,
                EffectiveTo = s.EffectiveTo,
                // IsActive = true trong DB  VÀ  hôm nay nằm trong khoảng hiệu lực
                IsActive = s.IsActive
                                 && s.EffectiveFrom.Date <= today
                                 && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value.Date >= today),
                LabName = labs.FirstOrDefault(l => l.LabId == s.LabId)?.LabName ?? "",
                InstructorName = users.FirstOrDefault(u => u.UserId == s.InstructorId)?.FullName ?? ""
            }).ToList();

            if (role == "Admin") await PopulateDropdowns();

            return View(vm);
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new ScheduleCreateViewModel { EffectiveFrom = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(ScheduleCreateViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }

            var sched = new LabSchedule
            {
                LabId = vm.LabId,
                InstructorId = vm.InstructorId,
                SubjectName = vm.SubjectName,
                DayOfWeek = vm.DayOfWeek,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                EffectiveFrom = vm.EffectiveFrom,
                EffectiveTo = vm.EffectiveTo,
                IsActive = true
            };

            var (success, errors) = await _scheduleService.CreateScheduleAsync(sched);
            if (!success)
            {
                foreach (var e in errors) ModelState.AddModelError("", e);
                await PopulateDropdowns(); return View(vm);
            }

            await _notify.SendAsync(vm.InstructorId, "InApp", "New Schedule Assigned",
                $"You have been assigned to teach {vm.SubjectName} on day {vm.DayOfWeek}.", "LabSchedule", sched.ScheduleId);
            await _notify.SendAsync(vm.InstructorId, "SMS", "New Schedule",
                $"New: {vm.SubjectName} Day {vm.DayOfWeek} {vm.StartTime:hh\\:mm}-{vm.EndTime:hh\\:mm}", "LabSchedule", sched.ScheduleId);

            TempData["Success"] = "Schedule created and instructor notified.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _uow.Schedules.GetByIdAsync(id);
            if (s == null) return NotFound();
            await PopulateDropdowns();
            return View(new ScheduleCreateViewModel
            {
                ScheduleId = s.ScheduleId,
                LabId = s.LabId,
                InstructorId = s.InstructorId,
                SubjectName = s.SubjectName,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                EffectiveFrom = s.EffectiveFrom,
                EffectiveTo = s.EffectiveTo
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(ScheduleCreateViewModel vm)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(vm); }
            var s = await _uow.Schedules.GetByIdAsync(vm.ScheduleId);
            if (s == null) return NotFound();
            s.LabId = vm.LabId; s.InstructorId = vm.InstructorId; s.SubjectName = vm.SubjectName;
            s.DayOfWeek = vm.DayOfWeek; s.StartTime = vm.StartTime; s.EndTime = vm.EndTime;
            s.EffectiveFrom = vm.EffectiveFrom; s.EffectiveTo = vm.EffectiveTo;
            _uow.Schedules.Update(s);
            await _uow.SaveChangesAsync();

            await _notify.SendAsync(vm.InstructorId, "InApp", "Schedule Updated",
                $"Your schedule for {vm.SubjectName} has been updated.", "LabSchedule", s.ScheduleId);
            await _notify.SendAsync(vm.InstructorId, "SMS", "Schedule Updated",
                $"Updated: {vm.SubjectName} Day {vm.DayOfWeek} {vm.StartTime:hh\\:mm}-{vm.EndTime:hh\\:mm}", "LabSchedule", s.ScheduleId);

            TempData["Success"] = "Schedule updated and instructor notified.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var s = await _uow.Schedules.GetByIdAsync(id);
            if (s != null)
            {
                s.IsActive = false;
                _uow.Schedules.Update(s);
                await _uow.SaveChangesAsync();
                await _notify.SendAsync(s.InstructorId, "InApp", "Schedule Cancelled",
                    $"Your schedule for {s.SubjectName} has been cancelled.", "LabSchedule", s.ScheduleId);
            }
            TempData["Success"] = "Schedule cancelled.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            var instructors = await _uow.Users.FindAsync(u => u.Role.RoleName == "Instructor" && u.IsActive);
            ViewBag.Instructors = new SelectList(instructors, "UserId", "FullName");
            ViewBag.Labs = new SelectList(await _uow.Labs.FindAsync(l => l.IsActive), "LabId", "LabName");
        }
    }

    // ── Lightweight list ViewModel (local) ───────────────────────────────────
    public class ScheduleListItem
    {
        public int ScheduleId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public int DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public string LabName { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
    }
}