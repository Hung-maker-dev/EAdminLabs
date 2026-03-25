using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Phần mềm được cài đặt trong Lab, theo dõi hạn bản quyền.
    /// Khi LicenseExpiry gần đến, NotificationService sẽ gửi cảnh báo.
    /// </summary>
    public class Software
    {
        public int SoftwareId { get; set; }
        public string SoftwareName { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? LicenseKey { get; set; }
        public DateTime? LicenseExpiry { get; set; }
        public string? InstallGuideUrl { get; set; }
        public int? LabId { get; set; }                     // NULL = cài toàn trường
        public bool IsNotificationSent { get; set; } = false; // Đã gửi cảnh báo?
        public string? Notes { get; set; }

        // Navigation
        public Lab? Lab { get; set; }
    }
}
