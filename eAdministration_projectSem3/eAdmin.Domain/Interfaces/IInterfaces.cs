using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;

namespace eAdmin.Domain.Interfaces
{
    // =====================================================================
    // IRepository<T>  –  Generic CRUD interface cho mọi Entity
    // =====================================================================
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
     
        Task<IEnumerable<T>> GetAllAsync(
            params Expression<Func<T, object>>[] includes);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        IQueryable<T> Query();

    }

    // =====================================================================
    // IUnitOfWork  –  Gom tất cả Repository, commit trong một transaction
    // =====================================================================
    public interface IUnitOfWork : IDisposable
    {
        IRepository<User>             Users             { get; }
        IRepository<Role>             Roles             { get; }
        IRepository<Department>       Departments       { get; }
        IRepository<Lab>              Labs              { get; }
        IRepository<Equipment>        Equipments        { get; }
        IRepository<EquipmentType>    EquipmentTypes    { get; }
        IRepository<Software>         Softwares         { get; }
        IRepository<Complaint>        Complaints        { get; }
        IRepository<ComplaintType>    ComplaintTypes    { get; }
        IRepository<LabSchedule>      Schedules         { get; }
        IRepository<ExtraLabRequest>  ExtraLabRequests  { get; }
        IRepository<LearningMaterial> LearningMaterials { get; }
        IRepository<Notification>     Notifications     { get; }
        IRepository<AuditLog>         AuditLogs         { get; }
        Task<int> SaveChangesAsync();
    }

    // =====================================================================
    // IComplaintService
    // =====================================================================
    public interface IComplaintService
    {
        Task<Complaint> CreateAndAutoAssignAsync(Complaint complaint);
        Task<bool> UpdateStatusAsync(int complaintId, string newStatus, int updatedByUserId);
        Task<IEnumerable<Complaint>> GetComplaintsByLabAsync(int labId);
        Task<IEnumerable<Complaint>> GetComplaintsByUserAsync(int userId);
        Task<IEnumerable<Complaint>> GetComplaintsByDepartmentAsync(int departmentId);
    }

    // =====================================================================
    // IScheduleService
    // =====================================================================
    public interface IScheduleService
    {
        Task<List<string>> CheckConflictsAsync(LabSchedule schedule);
        Task<(bool Success, List<string> Errors)> CreateScheduleAsync(LabSchedule schedule);
        Task<IEnumerable<LabSchedule>> GetScheduleByLabAsync(int labId);
        Task<IEnumerable<LabSchedule>> GetScheduleByInstructorAsync(int instructorId);
    }

    // =====================================================================
    // INotificationService
    // =====================================================================
    public interface INotificationService
    {
        Task SendAsync(int recipientId, string type, string subject, string message,
            string? entityType = null, int? entityId = null);
        Task CheckSoftwareExpiryAndNotifyAsync(int daysBeforeExpiry = 30);
    }

    // =====================================================================
    // IReportService
    // =====================================================================
    public interface IReportService
    {
        Task<IEnumerable<Equipment>> GetEquipmentConditionReportAsync(int? labId = null);
        Task<IEnumerable<Complaint>> GetComplaintReportAsync(DateTime from, DateTime to);
        Task<IEnumerable<Software>> GetSoftwareExpiryReportAsync();
    }
}
