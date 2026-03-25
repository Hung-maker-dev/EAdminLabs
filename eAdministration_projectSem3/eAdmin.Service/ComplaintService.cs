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
    /// Handles all Complaint business logic.
    /// Auto-assign algorithm: Least-Loaded TechStaff.
    /// Equipment condition is auto-updated based on complaint priority and status.
    /// </summary>
    public class ComplaintService : IComplaintService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;
        private readonly ILogger<ComplaintService> _logger;

        public ComplaintService(
            IUnitOfWork uow,
            INotificationService notify,
            ILogger<ComplaintService> logger)
        {
            _uow = uow;
            _notify = notify;
            _logger = logger;
        }

        // ── CREATE & AUTO-ASSIGN ───────────────────────────────────────────
        /// <summary>
        /// Creates a new complaint, auto-assigns to the least-loaded TechStaff,
        /// and immediately updates equipment condition based on priority.
        /// </summary>
        public async Task<Complaint> CreateAndAutoAssignAsync(Complaint complaint)
        {
            // 1. Set initial state
            complaint.Status = "Pending";
            complaint.CreatedAt = DateTime.UtcNow;

            await _uow.Complaints.AddAsync(complaint);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Complaint #{Id} created by User #{UserId}",
                complaint.ComplaintId, complaint.ReportedBy);

            // 2. Get all active TechStaff
            var allTechStaff = await _uow.Users.FindAsync(
                u => u.Role.RoleName == "TechStaff" && u.IsActive);

            if (!allTechStaff.Any())
            {
                _logger.LogWarning("No active TechStaff found. Complaint #{Id} stays Pending.",
                    complaint.ComplaintId);
                return complaint;
            }

            // 3. Count open complaints per TechStaff (Least-Loaded Algorithm)
            var openComplaints = await _uow.Complaints.FindAsync(
                c => c.AssignedTo.HasValue &&
                     c.Status != "Resolved" &&
                     c.Status != "Closed");

            // 4. Select TechStaff with fewest open complaints
            var selectedStaff = allTechStaff
                .OrderBy(s => openComplaints.Count(c => c.AssignedTo == s.UserId))
                .First();

            // 5. Assign and update status
            complaint.AssignedTo = selectedStaff.UserId;
            complaint.Status = "Assigned";
            _uow.Complaints.Update(complaint);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Complaint #{Id} assigned to TechStaff #{StaffId}",
                complaint.ComplaintId, selectedStaff.UserId);

            // 6. Auto-update equipment condition based on complaint priority
            //    Low / Medium  → Fair   (device still usable but has issues)
            //    High / Critical → Poor (device significantly impaired)
            if (complaint.EquipmentId.HasValue)
            {
                var equipment = await _uow.Equipments.GetByIdAsync(complaint.EquipmentId.Value);
                if (equipment != null)
                {
                    equipment.Condition = complaint.Priority switch
                    {
                        "Low" => "Fair",
                        "Medium" => "Fair",
                        "High" => "Poor",
                        "Critical" => "Poor",
                        _ => equipment.Condition
                    };
                    _uow.Equipments.Update(equipment);
                    await _uow.SaveChangesAsync();

                    _logger.LogInformation(
                        "Equipment #{EqId} condition updated to '{Condition}' " +
                        "based on complaint priority '{Priority}'",
                        equipment.EquipmentId, equipment.Condition, complaint.Priority);
                }
            }

            // 7. Notify assigned TechStaff — InApp + SMS
            await _notify.SendAsync(
                selectedStaff.UserId, "InApp",
                "New Complaint Assigned",
                $"Complaint #{complaint.ComplaintId}: \"{complaint.Title}\" has been assigned to you. " +
                $"Priority: {complaint.Priority}",
                "Complaint", complaint.ComplaintId);

            await _notify.SendAsync(
                selectedStaff.UserId, "SMS",
                "New Complaint Assigned",
                $"[eAdmin] Complaint #{complaint.ComplaintId} ({complaint.Priority}): " +
                $"{complaint.Title} — assigned to you.",
                "Complaint", complaint.ComplaintId);

            // 8. Notify reporter — confirmation
            await _notify.SendAsync(
                complaint.ReportedBy, "InApp",
                "Complaint Received",
                $"Your complaint #{complaint.ComplaintId} \"{complaint.Title}\" has been received " +
                $"and assigned to a technician.",
                "Complaint", complaint.ComplaintId);

            return complaint;
        }

        // ── UPDATE STATUS ─────────────────────────────────────────────────
        /// <summary>
        /// Updates complaint status.
        /// Note: Equipment condition update on status change is handled in
        /// ComplaintController.UpdateStatus (so TechStaff note can be included).
        /// This method is kept for interface compatibility.
        /// </summary>
        public async Task<bool> UpdateStatusAsync(int complaintId, string newStatus, int updatedByUserId)
        {
            var complaint = await _uow.Complaints.GetByIdAsync(complaintId);
            if (complaint == null) return false;

            complaint.Status = newStatus;
            if (newStatus == "Resolved" || newStatus == "Closed")
                complaint.ResolvedAt = DateTime.UtcNow;

            _uow.Complaints.Update(complaint);

            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                UserId = updatedByUserId,
                Action = "UpdateComplaintStatus",
                EntityType = "Complaint",
                EntityId = complaintId,
                Details = $"{{\"NewStatus\":\"{newStatus}\"}}",
                CreatedAt = DateTime.UtcNow
            });

            await _uow.SaveChangesAsync();

            if (newStatus == "Resolved" || newStatus == "Closed")
                await _notify.SendAsync(
                    complaint.ReportedBy, "InApp",
                    $"Complaint #{complaintId} {newStatus}",
                    $"Your complaint \"{complaint.Title}\" has been marked as {newStatus}.",
                    "Complaint", complaintId);

            return true;
        }

        // ── QUERIES ────────────────────────────────────────────────────────
        public async Task<IEnumerable<Complaint>> GetComplaintsByLabAsync(int labId)
            => await _uow.Complaints.FindAsync(c => c.LabId == labId);

        public async Task<IEnumerable<Complaint>> GetComplaintsByUserAsync(int userId)
            => await _uow.Complaints.FindAsync(
                c => c.ReportedBy == userId || c.AssignedTo == userId);

        public async Task<IEnumerable<Complaint>> GetComplaintsByDepartmentAsync(int departmentId)
        {
            var deptUsers = await _uow.Users.FindAsync(u => u.DepartmentId == departmentId);
            var userIds = deptUsers.Select(u => u.UserId).ToHashSet();
            return await _uow.Complaints.FindAsync(c => userIds.Contains(c.ReportedBy));
        }
    }
}