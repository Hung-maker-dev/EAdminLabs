using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Nhật ký hành động của người dùng (Login, Create, Update, Delete).
    /// Ghi lại để phục vụ audit và phục hồi dữ liệu.
    /// </summary>
    public class AuditLog
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? Details { get; set; }   // JSON
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
