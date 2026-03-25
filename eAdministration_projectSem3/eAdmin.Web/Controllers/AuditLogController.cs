using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            if (filter.Action == "Index")
                filter.Action = null;
            // ✅ Query trực tiếp DB (KHÔNG dùng GetAllAsync nữa)
            var logs = _uow.AuditLogs.Query();
            var users = await _uow.Users.GetAllAsync();

            // ── Filters ───────────────────────────────────────────────────
            // ── Filters (FIX) ─────────────────────────────────────────────

            // Username
            if (!string.IsNullOrWhiteSpace(filter.Username))
            {
                var matchIds = users
                    .Where(u => u.Username != null && u.Username.Contains(filter.Username))
                    .Select(u => (int?)u.UserId)
                    .ToHashSet();

                if (matchIds.Count > 0) // ⭐ quan trọng
                    logs = logs.Where(l => matchIds.Contains(l.UserId));
            }

            // Action (không phân biệt hoa thường)
            if (!string.IsNullOrWhiteSpace(filter.Action))
            {
                var action = filter.Action.ToLower();
                logs = logs.Where(l => l.Action != null && l.Action.ToLower().Contains(action));
            }

            // EntityType
            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                logs = logs.Where(l => l.EntityType == filter.EntityType);

            // Date
            if (filter.FromDate.HasValue)
                logs = logs.Where(l => l.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                logs = logs.Where(l => l.CreatedAt < filter.ToDate.Value.AddDays(1));
            // 🔥 Tổng số record
            filter.TotalRecords = await logs.CountAsync();

            // 🔥 Phân trang
            var logList = await logs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            // ── Map ───────────────────────────────────────────────────────
            filter.Results = logList.Select(l =>
            {
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
            }).ToList();

            return View(filter);
        }
    }
}