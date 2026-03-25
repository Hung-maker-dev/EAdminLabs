using System;
using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Thiết bị phần cứng trong phòng Lab (máy tính, máy in, LCD, AC, v.v.)
    /// </summary>
    public class Equipment
    {
        public int EquipmentId { get; set; }
        public int LabId { get; set; }
        public int EquipmentTypeId { get; set; }
        public string AssetCode { get; set; } = string.Empty;   // Mã tài sản duy nhất
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public DateTime? WarrantyExpiry { get; set; }

        /// <summary>Good | Fair | Poor | OutOfService</summary>
        public string Condition { get; set; } = "Good";
        public string? Notes { get; set; }

        // Navigation
        public Lab Lab { get; set; } = null!;
        public EquipmentType EquipmentType { get; set; } = null!;
        public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    }
}
