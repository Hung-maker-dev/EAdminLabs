using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace eAdmin.Service
{
    /// <summary>
    /// Service quản lý thời khóa biểu phòng Lab.
    /// Kiểm tra 3 loại xung đột trước khi tạo lịch:
    ///   (1) Phòng Lab đã có lịch trùng slot
    ///   (2) Giảng viên đã có lịch trùng slot
    ///   (3) Khoảng thời gian overlap (StartTime - EndTime)
    /// </summary>
    public class ScheduleService : IScheduleService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ScheduleService> _logger;

        public ScheduleService(IUnitOfWork uow, ILogger<ScheduleService> logger)
        {
            _uow    = uow;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        // KIỂM TRA XUNG ĐỘT LỊCH
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Kiểm tra xung đột. Trả về danh sách lỗi (list rỗng = không xung đột).
        /// </summary>
        public async Task<List<string>> CheckConflictsAsync(LabSchedule newSched)
        {
            var errors = new List<string>();

            // Lấy tất cả lịch active cùng thứ trong tuần, trừ chính nó (khi edit)
            var existing = await _uow.Schedules.FindAsync(s =>
                s.IsActive &&
                s.DayOfWeek == newSched.DayOfWeek &&
                s.ScheduleId != newSched.ScheduleId &&
                // Khoảng ngày hiệu lực có overlap
                (s.EffectiveTo == null  || s.EffectiveTo  >= newSched.EffectiveFrom) &&
                (newSched.EffectiveTo == null || newSched.EffectiveTo >= s.EffectiveFrom)
            );

            foreach (var s in existing)
            {
                // Kiểm tra time overlap: [A.Start, A.End) ∩ [B.Start, B.End) ≠ ∅
                bool timeOverlap = newSched.StartTime < s.EndTime &&
                                   newSched.EndTime   > s.StartTime;
                if (!timeOverlap) continue;

                // ── Xung đột phòng Lab ─────────────────────────────────
                if (s.LabId == newSched.LabId)
                    errors.Add($"Phòng Lab đã có lịch '{s.SubjectName}' " +
                               $"({s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}) vào thứ {s.DayOfWeek}.");

                // ── Xung đột giảng viên ────────────────────────────────
                if (s.InstructorId == newSched.InstructorId)
                    errors.Add($"Giảng viên đã có lịch dạy '{s.SubjectName}' " +
                               $"({s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}) vào thứ {s.DayOfWeek}.");
            }

            return errors;
        }

        // ─────────────────────────────────────────────────────────────────
        // TẠO LỊCH MỚI
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Tạo lịch mới sau khi kiểm tra xung đột.
        /// Trả về (true, []) nếu thành công, (false, [lỗi]) nếu có xung đột.
        /// </summary>
        public async Task<(bool Success, List<string> Errors)> CreateScheduleAsync(LabSchedule sched)
        {
            var errors = await CheckConflictsAsync(sched);
            if (errors.Any())
            {
                _logger.LogWarning("Schedule conflict detected for Lab #{LabId}: {Errors}",
                    sched.LabId, string.Join("; ", errors));
                return (false, errors);
            }

            sched.IsActive = true;
            await _uow.Schedules.AddAsync(sched);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Schedule #{Id} created for Lab #{LabId}", sched.ScheduleId, sched.LabId);
            return (true, new List<string>());
        }

        public async Task<IEnumerable<LabSchedule>> GetScheduleByLabAsync(int labId)
            => await _uow.Schedules.FindAsync(s => s.LabId == labId && s.IsActive);

        public async Task<IEnumerable<LabSchedule>> GetScheduleByInstructorAsync(int instructorId)
            => await _uow.Schedules.FindAsync(s => s.InstructorId == instructorId && s.IsActive);
    }
}
