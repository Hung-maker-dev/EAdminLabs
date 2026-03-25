using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// In-app / SMS notification stored in DB.
    /// NotificationType: "InApp" | "SMS"
    /// </summary>
    public class Notification
    {
        public int NotificationId { get; set; }
        public int RecipientUserId { get; set; }
        public string NotificationType { get; set; } = "InApp";
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public bool IsSent { get; set; } = false;
        public bool IsRead { get; set; } = false;
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User Recipient { get; set; } = null!;
    }
}
