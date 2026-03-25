using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Yêu cầu mượn thêm phòng Lab của HOD.
    /// Admin xem slot trống và phản hồi Approved/Rejected.
    /// </summary>
    public class ExtraLabRequest
    {
        public int RequestId { get; set; }
        public int RequestedBy { get; set; }
        public int? LabId { get; set; }
        public DateTime RequestDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Purpose { get; set; }

        /// <summary>Pending | Approved | Rejected</summary>
        public string Status { get; set; } = "Pending";
        public string? AdminReply { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User Requester { get; set; } = null!;
        public Lab? Lab { get; set; }
    }
}
