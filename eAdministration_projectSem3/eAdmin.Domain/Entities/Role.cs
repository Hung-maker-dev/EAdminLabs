using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Vai trò hệ thống: Admin, HOD, Instructor, TechStaff, Student
    /// </summary>
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
