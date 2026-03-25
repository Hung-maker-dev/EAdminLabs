using System;

namespace eAdmin.Domain.Entities
{
    /// <summary>
    /// Thời khóa biểu phòng Lab.
    /// ScheduleService kiểm tra xung đột phòng và giảng viên trước khi lưu.
    /// </summary>
    public class LabSchedule
    {
        public int ScheduleId { get; set; }
        public int LabId { get; set; }
        public int InstructorId { get; set; }
        public string SubjectName { get; set; } = string.Empty;

        /// <summary>1=Thứ Hai ... 7=Chủ Nhật</summary>
        public int DayOfWeek { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation
        public Lab Lab { get; set; } = null!;
        public User Instructor { get; set; } = null!;
    }
}
