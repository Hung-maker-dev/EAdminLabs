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
    public class NotificationController : Controller
    {
        private readonly IUnitOfWork _uow;
        public NotificationController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index()
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifs = await _uow.Notifications.FindAsync(n => n.RecipientUserId == uid);
            var vm = notifs.OrderByDescending(n => n.CreatedAt).Select(n => new NotificationViewModel
            {
                NotificationId = n.NotificationId, Subject = n.Subject, Message = n.Message,
                NotificationType = n.NotificationType, IsSent = n.IsSent, IsRead = n.IsRead,
                CreatedAt = n.CreatedAt, RelatedEntityType = n.RelatedEntityType, RelatedEntityId = n.RelatedEntityId
            }).ToList();
            return View(vm);
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var n = await _uow.Notifications.GetByIdAsync(id);
            if (n != null && n.RecipientUserId == uid)
            {
                n.IsRead = true;
                _uow.Notifications.Update(n);
                await _uow.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifs = await _uow.Notifications.FindAsync(n => n.RecipientUserId == uid && !n.IsRead);
            foreach (var n in notifs) { n.IsRead = true; _uow.Notifications.Update(n); }
            await _uow.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // AJAX endpoint for bell icon unread count
        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifs = await _uow.Notifications.FindAsync(n => n.RecipientUserId == uid && !n.IsRead);
            return Json(new { count = notifs.Count() });
        }

        // AJAX endpoint for bell dropdown (latest 5)
        [HttpGet]
        public async Task<IActionResult> Latest()
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var notifs = await _uow.Notifications.FindAsync(n => n.RecipientUserId == uid);
            var vm = notifs.OrderByDescending(n => n.CreatedAt).Take(5).Select(n => new
            {
                n.NotificationId, n.Subject, n.Message, n.IsRead,
                CreatedAt = n.CreatedAt.ToString("dd/MM HH:mm"),
                n.RelatedEntityType, n.RelatedEntityId
            });
            return Json(vm);
        }
    }
}
