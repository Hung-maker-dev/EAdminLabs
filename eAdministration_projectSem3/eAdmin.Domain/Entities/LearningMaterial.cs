using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Learning material: syllabus, install guide, e-learning content, server info.
    /// Accessible by Students (their dept + public ones).
    /// </summary>
    public class LearningMaterial
    {
        public int MaterialId { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>Syllabus | InstallGuide | ELearning | ServerInfo | Other</summary>
        public string MaterialType { get; set; } = "Other";

        // Optional text description
        public string? Description { get; set; }

        public string? FilePath { get; set; }
        public string? ExternalUrl { get; set; }
        public int? DepartmentId { get; set; }
        public int UploadedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPublic { get; set; } = true;

        // Navigation
        public Department? Department { get; set; }
        public User Uploader { get; set; } = null!;
    }
}
