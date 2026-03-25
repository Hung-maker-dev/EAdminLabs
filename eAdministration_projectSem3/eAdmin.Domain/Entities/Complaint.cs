using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Khiếu nại / sự cố thiết bị trong phòng Lab.
    /// Quy trình trạng thái: Pending → Assigned → InProgress → Resolved → Closed
    /// </summary>
    public class Complaint
    {
        public int ComplaintId { get; set; }
        public int LabId { get; set; }
        public int? EquipmentId { get; set; }
        public int ReportedBy { get; set; }
        public int? AssignedTo { get; set; }
        public int ComplaintTypeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>Pending | Assigned | InProgress | Resolved | Closed</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Low | Medium | High | Critical</summary>
        public string Priority { get; set; } = "Medium";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        // Navigation
        public Lab Lab { get; set; } = null!;
        public Equipment? Equipment { get; set; }
        public User Reporter { get; set; } = null!;
        public User? Assignee { get; set; }
        public ComplaintType ComplaintType { get; set; } = null!;
    }
}
