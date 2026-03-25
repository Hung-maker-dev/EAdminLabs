using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Phòng Lab máy tính trong tổ chức
    /// </summary>
    public class Lab
    {
        public int LabId { get; set; }
        public string LabName { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int Capacity { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }

        // Navigation
        public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
        public ICollection<LabSchedule> LabSchedules { get; set; } = new List<LabSchedule>();
        public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
        public ICollection<Software> Softwares { get; set; } = new List<Software>();
        public ICollection<ExtraLabRequest> ExtraLabRequests { get; set; } = new List<ExtraLabRequest>();
    }
}
