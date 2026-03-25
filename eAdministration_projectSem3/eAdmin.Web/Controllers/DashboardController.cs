using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IUnitOfWork _uow;
        public DashboardController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var uid  = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = User.FindFirst("Role")?.Value ?? "";

            var labs       = await _uow.Labs.GetAllAsync();
            var equips     = await _uow.Equipments.GetAllAsync();
            var complaints = await _uow.Complaints.GetAllAsync();
            var softwares  = await _uow.Softwares.GetAllAsync();
            var notifs     = await _uow.Notifications.FindAsync(n => n.RecipientUserId == uid);
            var requests   = await _uow.ExtraLabRequests.FindAsync(r => r.Status == "Pending");
            var users      = await _uow.Users.GetAllAsync();
            var types      = await _uow.ComplaintTypes.GetAllAsync();

            // Role-filtered complaints for "Recent Complaints"
            var myComplaints = role switch
            {
                "Admin"      => complaints,
                "HOD"        => complaints.Where(c => {
                                    var deptClaim = User.FindFirst("DepartmentId")?.Value;
                                    if (!int.TryParse(deptClaim, out var dId)) return false;
                                    return users.FirstOrDefault(u => u.UserId == c.ReportedBy)?.DepartmentId == dId;
                                }),
                "TechStaff"  => complaints.Where(c => c.AssignedTo == uid),
                "Instructor" => complaints.Where(c => c.ReportedBy == uid),
                _            => Enumerable.Empty<Domain.Entities.Complaint>()
            };

            var vm = new DashboardViewModel
            {
                TotalLabs             = labs.Count(),
                TotalEquipments       = equips.Count(),
                OpenComplaints        = complaints.Count(c => c.Status != "Closed" && c.Status != "Resolved"),
                PendingRequests       = requests.Count(),
                ExpiringSoftwareCount = softwares.Count(s => s.LicenseExpiry.HasValue && s.LicenseExpiry.Value.Date <= DateTime.Today.AddDays(30)),
                UnreadNotifications   = notifs.Count(n => !n.IsRead),
                RecentComplaints = myComplaints.OrderByDescending(c => c.CreatedAt).Take(8).Select(c => new ComplaintListViewModel
                {
                    ComplaintId  = c.ComplaintId,
                    Title        = c.Title,
                    Status       = c.Status,
                    Priority     = c.Priority,
                    LabName      = labs.FirstOrDefault(l => l.LabId == c.LabId)?.LabName ?? "",
                    TypeName     = types.FirstOrDefault(t => t.ComplaintTypeId == c.ComplaintTypeId)?.TypeName ?? "",
                    ReporterName = users.FirstOrDefault(u => u.UserId == c.ReportedBy)?.FullName ?? "",
                    AssigneeName = c.AssignedTo.HasValue ? users.FirstOrDefault(u => u.UserId == c.AssignedTo)?.FullName : null,
                    CreatedAt    = c.CreatedAt,
                    ResolvedAt   = c.ResolvedAt
                }).ToList(),
                RecentNotifications = notifs.OrderByDescending(n => n.CreatedAt).Take(5).Select(n => new NotificationViewModel
                {
                    NotificationId   = n.NotificationId,
                    Subject          = n.Subject,
                    Message          = n.Message,
                    NotificationType = n.NotificationType,
                    IsSent           = n.IsSent,
                    IsRead           = n.IsRead,
                    CreatedAt        = n.CreatedAt
                }).ToList()
            };

            return View(vm);
        }
    }
}
