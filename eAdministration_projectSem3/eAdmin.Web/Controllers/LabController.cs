using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

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
            await _uow.Labs.AddAsync(lab);
            await _uow.SaveChangesAsync();
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
            _uow.Labs.Update(lab);
            await _uow.SaveChangesAsync();
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
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            request.RequestedBy = userId; request.Status = "Pending";
            await _uow.ExtraLabRequests.AddAsync(request);
            await _uow.SaveChangesAsync();

            // Notify all Admins
            var admins = await _uow.Users.FindAsync(u => u.Role.RoleName == "Admin" && u.IsActive);
            foreach (var admin in admins)
                await _notify.SendAsync(admin.UserId, "InApp", "New Extra Lab Request",
                    $"HOD submitted an extra lab request for {request.RequestDate:dd/MM/yyyy}.", "ExtraLabRequest", request.RequestId);

            TempData["Success"] = "Request submitted. Admins have been notified.";
            return RedirectToAction("Index", "Dashboard");
        }

        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> ExtraRequests() => View(await _uow.ExtraLabRequests.GetAllAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin")]
        public async Task<IActionResult> ReplyRequest(int id, string status, string? reply)
        {
            var req = await _uow.ExtraLabRequests.GetByIdAsync(id);
            if (req == null) return NotFound();
            req.Status = status; req.AdminReply = reply;
            _uow.ExtraLabRequests.Update(req);
            await _uow.SaveChangesAsync();

            // Notify HOD of decision
            var msg = status == "Approved"
                ? $"Your extra lab request for {req.RequestDate:dd/MM/yyyy} has been APPROVED. {reply}"
                : $"Your extra lab request for {req.RequestDate:dd/MM/yyyy} has been REJECTED. Reason: {reply}";
            await _notify.SendAsync(req.RequestedBy, "InApp",
                $"Extra Lab Request {status}", msg, "ExtraLabRequest", req.RequestId);
            await _notify.SendAsync(req.RequestedBy, "SMS",
                $"Extra Lab Request {status}", msg, "ExtraLabRequest", req.RequestId);

            TempData["Success"] = $"Request {(status == "Approved" ? "approved" : "rejected")} and HOD notified.";
            return RedirectToAction(nameof(ExtraRequests));
        }
    }
}
