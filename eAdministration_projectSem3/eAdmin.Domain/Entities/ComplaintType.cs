using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Loại sự cố: Hardware | Software | Network | Power | Other
    /// Được dùng để auto-assign complaint cho đúng TechStaff
    /// </summary>
    public class ComplaintType
    {
        public int ComplaintTypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    }
}
