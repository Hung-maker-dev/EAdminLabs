using System;
using System.Collections.Generic;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Department / Faculty in the educational organization.
    /// </summary>
    public class Department
    {
        public int DepartmentId { get; set; }

        // Primary name field used by controllers and views
        public string DepartmentName { get; set; } = string.Empty;

        // Short code e.g. "CS", "IT", "EE"
        public string? Code { get; set; }

        public int? HodUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? HodUser { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<LearningMaterial> LearningMaterials { get; set; } = new List<LearningMaterial>();
    }
}
