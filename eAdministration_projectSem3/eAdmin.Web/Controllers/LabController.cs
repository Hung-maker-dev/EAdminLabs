using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    public class LabController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;
        public LabController(IUnitOfWork uow, INotificationService notify) { _uow = uow; _notify = notify; }

        public async Task<IActionResult> Index() => View(await _uow.Labs.GetAllAsync());

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public IActionResult Create() => View(new Lab());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Create(Lab lab)
        {
            if (!ModelState.IsValid) return View(lab);

            // Kiểm tra trùng LabName
            var exists = (await _uow.Labs.FindAsync(l =>
                l.LabName.ToLower() == lab.LabName.ToLower().Trim()
            )).Any();

            if (exists)
            {
                ModelState.AddModelError("LabName", "A lab with this name already exists.");
                return View(lab);
            }

            lab.LabName = lab.LabName.Trim();
            await _uow.Labs.AddAsync(lab);

            try
            {
                await _uow.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                ModelState.AddModelError("LabName", "A lab with this name already exists.");
                return View(lab);
            }

            TempData["Success"] = "Lab added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var lab = await _uow.Labs.GetByIdAsync(id);
            if (lab == null) return NotFound();
            return View(lab);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> Edit(Lab lab)
        {
            if (!ModelState.IsValid) return View(lab);

            // Kiểm tra trùng với lab khác (loại trừ chính nó)
            var exists = (await _uow.Labs.FindAsync(l =>
                l.LabName.ToLower() == lab.LabName.ToLower().Trim() &&
                l.LabId != lab.LabId
            )).Any();

            if (exists)
            {
                ModelState.AddModelError("LabName", "A lab with this name already exists.");
                return View(lab);
            }

            lab.LabName = lab.LabName.Trim();
            _uow.Labs.Update(lab);

            try
            {
                await _uow.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                ModelState.AddModelError("LabName", "A lab with this name already exists.");
                return View(lab);
            }

            TempData["Success"] = "Lab updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var lab = await _uow.Labs.GetByIdAsync(id);
            if (lab != null) { lab.IsActive = !lab.IsActive; _uow.Labs.Update(lab); await _uow.SaveChangesAsync(); }
            TempData["Success"] = "Lab status updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [AuthorizeRoles("HOD")]
        public async Task<IActionResult> RequestExtra()
        {
            ViewBag.Labs = new SelectList(await _uow.Labs.FindAsync(l => l.IsActive), "LabId", "LabName");
            return View(new ExtraLabRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("HOD")]
        public async Task<IActionResult> RequestExtra(ExtraLabRequest request)
        {
            async Task LoadLabs()
            {
                ViewBag.Labs = new SelectList(await _uow.Labs.FindAsync(l => l.IsActive), "LabId", "LabName");
            }

            // Gán RequestedBy TRƯỚC validation
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            request.RequestedBy = userId;

            // Xoá lỗi navigation property không liên quan đến form
            ModelState.Remove("Requester");
            ModelState.Remove("Lab");

            if (!ModelState.IsValid)
            {
                await LoadLabs();
                return View(request);
            }

            // Date validation
            if (request.RequestDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("RequestDate", "Date cannot be in the past.");
                await LoadLabs();
                return View(request);
            }

            // Time validation
            if (request.StartTime >= request.EndTime)
            {
                ModelState.AddModelError("", "End time must be after start time.");
                await LoadLabs();
                return View(request);
            }

            // Check trùng lịch Extra
            var conflict = await _uow.ExtraLabRequests.FindAsync(r =>
                r.LabId == request.LabId &&
                r.RequestDate.Date == request.RequestDate.Date &&
                r.Status == "Approved" &&
                request.StartTime < r.EndTime &&
                request.EndTime > r.StartTime
            );

            if (conflict.Any())
            {
                ModelState.AddModelError("", "This time slot is already booked.");
                await LoadLabs();
                return View(request);
            }

            request.Status = "Pending";
            await _uow.ExtraLabRequests.AddAsync(request);
            await _uow.SaveChangesAsync();

            TempData["Success"] = "Request submitted. Admins have been notified.";
            return RedirectToAction("Index", "Dashboard");
        }

        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> ExtraRequests() => View(await _uow.ExtraLabRequests.GetAllAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> ReplyRequest(int id, string status, string? reply, bool forceApprove = false)
        {
            var req = await _uow.ExtraLabRequests.GetByIdAsync(id);
            if (req == null) return NotFound();

            // Chặn approve nếu đã hết hạn (tính cả ngày + giờ kết thúc)
            if (status == "Approved" && DateTime.Now > req.RequestDate.Date.Add(req.EndTime))
            {
                TempData["Error"] = "Cannot approve an expired request.";
                return RedirectToAction(nameof(ExtraRequests));
            }

            // Kiểm tra và xử lý conflict với LabSchedule thường khi Approve
            if (status == "Approved")
            {
                // Chuyển đổi DayOfWeek: Sunday=0 → 7, Monday=1 → 1, ..., Saturday=6 → 6
                var jsDay = (int)req.RequestDate.DayOfWeek;
                var dow = jsDay == 0 ? 7 : jsDay;

                var conflictSchedules = (await _uow.Schedules.FindAsync(s =>
                    s.LabId == req.LabId &&
                    s.IsActive &&
                    s.DayOfWeek == dow &&
                    s.StartTime < req.EndTime &&
                    s.EndTime > req.StartTime &&
                    s.EffectiveFrom.Date <= req.RequestDate.Date &&
                    (s.EffectiveTo == null || s.EffectiveTo.Value.Date >= req.RequestDate.Date)
                )).ToList();

                if (conflictSchedules.Any() && !forceApprove)
                {
                    // Lưu thông tin conflict vào TempData để hiển thị cảnh báo
                    TempData["ConflictRequestId"] = id;
                    TempData["ConflictReply"] = reply;
                    TempData["ConflictStatus"] = status;
                    TempData["ConflictScheduleIds"] = string.Join(",", conflictSchedules.Select(s => s.ScheduleId));
                    return RedirectToAction(nameof(ExtraRequests));
                }

                if (conflictSchedules.Any() && forceApprove)
                {
                    // Deactivate các lịch bị trùng và notify giảng viên
                    foreach (var s in conflictSchedules)
                    {
                        s.IsActive = false;
                        _uow.Schedules.Update(s);

                        var cancelMsg = $"Lịch học '{s.SubjectName}' vào {req.RequestDate:dd/MM/yyyy} " +
                                        $"({s.StartTime:hh\\:mm}–{s.EndTime:hh\\:mm}) đã bị huỷ " +
                                        $"do phòng được sử dụng cho buổi học bổ sung của Khoa.";

                        await _notify.SendAsync(s.InstructorId, "InApp",
                            "Lịch học bị huỷ", cancelMsg, "LabSchedule", s.ScheduleId);
                        await _notify.SendAsync(s.InstructorId, "SMS",
                            "Lịch học bị huỷ", cancelMsg, "LabSchedule", s.ScheduleId);
                    }
                }

                // Thêm vào LabSchedules: SubjectName = FullName HOD + Tên Khoa
                var requester = await _uow.Users.GetByIdAsync(req.RequestedBy);
                var department = requester?.DepartmentId != null
                    ? await _uow.Departments.GetByIdAsync(requester.DepartmentId.Value)
                    : null;

                var subjectName = requester?.FullName ?? "HOD";
                if (department != null)
                    subjectName += $" – {department.DepartmentName}";

                var schedule = new LabSchedule
                {
                    LabId = req.LabId!.Value,
                    InstructorId = req.RequestedBy,
                    SubjectName = subjectName,
                    DayOfWeek = dow,
                    StartTime = req.StartTime,
                    EndTime = req.EndTime,
                    EffectiveFrom = req.RequestDate.Date,
                    EffectiveTo = req.RequestDate.Date,   // chỉ có hiệu lực 1 ngày
                    IsActive = true
                };
                await _uow.Schedules.AddAsync(schedule);
            }

            req.Status = status;
            req.AdminReply = reply;
            _uow.ExtraLabRequests.Update(req);
            await _uow.SaveChangesAsync();

            // Notify HOD kết quả
            var hodMsg = status == "Approved"
                ? $"Your extra lab request for {req.RequestDate:dd/MM/yyyy} has been APPROVED. {reply}"
                : $"Your extra lab request for {req.RequestDate:dd/MM/yyyy} has been REJECTED. Reason: {reply}";

            await _notify.SendAsync(req.RequestedBy, "InApp",
                $"Extra Lab Request {status}", hodMsg, "ExtraLabRequest", req.RequestId);
            await _notify.SendAsync(req.RequestedBy, "SMS",
                $"Extra Lab Request {status}", hodMsg, "ExtraLabRequest", req.RequestId);

            TempData["Success"] = $"Request {(status == "Approved" ? "approved" : "rejected")} and HOD notified.";
            return RedirectToAction(nameof(ExtraRequests));
        }
    }
}