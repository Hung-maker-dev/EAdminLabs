using System;
using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Đại diện cho tất cả người dùng trong hệ thống.
    /// Roles: Admin | HOD | Instructor | TechStaff | Student
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Role Role { get; set; } = null!;
        public Department? Department { get; set; }
        public ICollection<Complaint> ReportedComplaints { get; set; } = new List<Complaint>();
        public ICollection<Complaint> AssignedComplaints { get; set; } = new List<Complaint>();
        public ICollection<LabSchedule> LabSchedules { get; set; } = new List<LabSchedule>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
