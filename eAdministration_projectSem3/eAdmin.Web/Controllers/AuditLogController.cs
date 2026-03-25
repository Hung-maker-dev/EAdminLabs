using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    [AuthorizeRoles("Admin")]
    public class AuditLogController : Controller
    {
        private readonly IUnitOfWork _uow;
        public AuditLogController(IUnitOfWork uow) => _uow = uow;

        public async Task<IActionResult> Index(AuditLogFilterViewModel filter)
        {
            var logs = await _uow.AuditLogs.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();

            // ── Filters ───────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(filter.Username))
            {
                var matchIds = users
                    .Where(u => u.Username.Contains(filter.Username))
                    .Select(u => (int?)u.UserId)   // cast to int? for comparison
                    .ToHashSet();

                logs = logs.Where(l => matchIds.Contains(l.UserId));
            }

            if (!string.IsNullOrEmpty(filter.Action))
                logs = logs.Where(l => l.Action.Contains(filter.Action));

            if (!string.IsNullOrEmpty(filter.EntityType))
                logs = logs.Where(l => l.EntityType == filter.EntityType);

            if (filter.FromDate.HasValue)
                logs = logs.Where(l => l.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                logs = logs.Where(l => l.CreatedAt <= filter.ToDate.Value.AddDays(1));

            // ── Map to ViewModel ──────────────────────────────────────────
            filter.Results = logs
                .OrderByDescending(l => l.CreatedAt)
                .Take(500)
                .Select(l =>
                {
                    // Fix: compare int? with int? by casting
                    var u = users.FirstOrDefault(u => (int?)u.UserId == l.UserId);
                    return new AuditLogViewModel
                    {
                        AuditLogId = l.LogId,
                        Action = l.Action,
                        EntityType = l.EntityType,
                        EntityId = l.EntityId,
                        Details = l.Details,
                        CreatedAt = l.CreatedAt,
                        UserFullName = u?.FullName ?? "System",
                        Username = u?.Username ?? "–"
                    };
                })
                .ToList();

            return View(filter);
        }
    }
}