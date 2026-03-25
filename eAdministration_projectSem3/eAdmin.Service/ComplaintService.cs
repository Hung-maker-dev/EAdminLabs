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
    /// Xử lý toàn bộ nghiệp vụ liên quan đến Complaint.
    /// Thuật toán auto-assign: Least-Loaded TechStaff
    /// (chọn nhân viên kỹ thuật đang có ít complaint mở nhất).
    /// </summary>
    public class ComplaintService : IComplaintService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;
        private readonly ILogger<ComplaintService> _logger;

        public ComplaintService(IUnitOfWork uow, INotificationService notify,
            ILogger<ComplaintService> logger)
        {
            _uow    = uow;
            _notify = notify;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        // TẠO COMPLAINT VÀ TỰ ĐỘNG PHÂN CÔNG
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Tạo complaint mới, sau đó tìm TechStaff phù hợp (least-loaded)
        /// và phân công tự động. Gửi thông báo InApp đến người được phân công.
        /// </summary>
        public async Task<Complaint> CreateAndAutoAssignAsync(Complaint complaint)
        {
            // 1. Khởi tạo trạng thái ban đầu
            complaint.Status    = "Pending";
            complaint.CreatedAt = DateTime.UtcNow;

            await _uow.Complaints.AddAsync(complaint);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Complaint #{Id} created by User #{UserId}",
                complaint.ComplaintId, complaint.ReportedBy);

            // 2. Lấy danh sách TechStaff đang hoạt động
            var allTechStaff = await _uow.Users.FindAsync(
                u => u.Role.RoleName == "TechStaff" && u.IsActive);

            if (!allTechStaff.Any())
            {
                _logger.LogWarning("No active TechStaff found. Complaint #{Id} stays Pending.",
                    complaint.ComplaintId);
                return complaint;
            }

            // 3. Đếm complaint đang mở của từng TechStaff
            var openComplaints = await _uow.Complaints.FindAsync(
                c => c.AssignedTo.HasValue
                  && c.Status != "Resolved"
                  && c.Status != "Closed");

            // 4. Chọn TechStaff ít việc nhất (Least-Loaded Algorithm)
            var selectedStaff = allTechStaff
                .OrderBy(s => openComplaints.Count(c => c.AssignedTo == s.UserId))
                .First();

            // 5. Gán và cập nhật trạng thái
            complaint.AssignedTo = selectedStaff.UserId;
            complaint.Status     = "Assigned";
            _uow.Complaints.Update(complaint);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Complaint #{Id} assigned to TechStaff #{StaffId}",
                complaint.ComplaintId, selectedStaff.UserId);

            // 6. Thông báo cho TechStaff được phân công
            await _notify.SendAsync(selectedStaff.UserId, "InApp",
                "Bạn có nhiệm vụ mới",
                $"Complaint #{complaint.ComplaintId}: {complaint.Title} đã được giao cho bạn.",
                "Complaint", complaint.ComplaintId);

            // 7. Thông báo xác nhận cho người gửi
            await _notify.SendAsync(complaint.ReportedBy, "InApp",
                "Khiếu nại đã tiếp nhận",
                $"Complaint #{complaint.ComplaintId} của bạn đã được tiếp nhận và phân công xử lý.",
                "Complaint", complaint.ComplaintId);

            return complaint;
        }

        // ─────────────────────────────────────────────────────────────────
        // CẬP NHẬT TRẠNG THÁI
        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Cập nhật trạng thái complaint. Tự động set ResolvedAt khi Resolved.
        /// </summary>
        public async Task<bool> UpdateStatusAsync(int complaintId, string newStatus, int updatedByUserId)
        {
            var complaint = await _uow.Complaints.GetByIdAsync(complaintId);
            if (complaint == null) return false;

            complaint.Status = newStatus;
            if (newStatus == "Resolved")
                complaint.ResolvedAt = DateTime.UtcNow;

            _uow.Complaints.Update(complaint);

            // Ghi audit log
            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                UserId     = updatedByUserId,
                Action     = "UpdateComplaintStatus",
                EntityType = "Complaint",
                EntityId   = complaintId,
                Details    = $"{{\"NewStatus\":\"{newStatus}\"}}"
            });

            await _uow.SaveChangesAsync();

            // Thông báo cho người gửi khi Resolved
            if (newStatus == "Resolved")
                await _notify.SendAsync(complaint.ReportedBy, "InApp",
                    "Complaint Resolved",
                    $"Your complaint #{complaintId} \"{complaint.Title}\" has been resolved.",
                    "Complaint", complaintId);

            return true;
        }

        public async Task<IEnumerable<Complaint>> GetComplaintsByLabAsync(int labId)
            => await _uow.Complaints.FindAsync(c => c.LabId == labId);

        public async Task<IEnumerable<Complaint>> GetComplaintsByUserAsync(int userId)
            => await _uow.Complaints.FindAsync(c => c.ReportedBy == userId || c.AssignedTo == userId);

        public async Task<IEnumerable<Complaint>> GetComplaintsByDepartmentAsync(int departmentId)
        {
            // Lấy complaint dựa trên người gửi thuộc department
            var deptUsers = await _uow.Users.FindAsync(u => u.DepartmentId == departmentId);
            var userIds = deptUsers.Select(u => u.UserId).ToHashSet();
            return await _uow.Complaints.FindAsync(c => userIds.Contains(c.ReportedBy));
        }
    }
}
