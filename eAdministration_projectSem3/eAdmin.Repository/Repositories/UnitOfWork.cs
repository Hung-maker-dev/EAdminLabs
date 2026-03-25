using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Repository.Data;

namespace eAdmin.Repository.Repositories
{
    /// <summary>
    /// Unit of Work – gom tất cả Repository vào một transaction.
    /// Gọi SaveChangesAsync() một lần duy nhất để commit tất cả thay đổi.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IRepository<User>             Users             { get; }
        public IRepository<Role>             Roles             { get; }
        public IRepository<Department>       Departments       { get; }
        public IRepository<Lab>              Labs              { get; }
        public IRepository<Equipment>        Equipments        { get; }
        public IRepository<EquipmentType>    EquipmentTypes    { get; }
        public IRepository<Software>         Softwares         { get; }
        public IRepository<Complaint>        Complaints        { get; }
        public IRepository<ComplaintType>    ComplaintTypes    { get; }
        public IRepository<LabSchedule>      Schedules         { get; }
        public IRepository<ExtraLabRequest>  ExtraLabRequests  { get; }
        public IRepository<LearningMaterial> LearningMaterials { get; }
        public IRepository<Notification>     Notifications     { get; }
        public IRepository<AuditLog>         AuditLogs         { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context          = context;
            Users             = new GenericRepository<User>(context);
            Roles             = new GenericRepository<Role>(context);
            Departments       = new GenericRepository<Department>(context);
            Labs              = new GenericRepository<Lab>(context);
            Equipments        = new GenericRepository<Equipment>(context);
            EquipmentTypes    = new GenericRepository<EquipmentType>(context);
            Softwares         = new GenericRepository<Software>(context);
            Complaints        = new GenericRepository<Complaint>(context);
            ComplaintTypes    = new GenericRepository<ComplaintType>(context);
            Schedules         = new GenericRepository<LabSchedule>(context);
            ExtraLabRequests  = new GenericRepository<ExtraLabRequest>(context);
            LearningMaterials = new GenericRepository<LearningMaterial>(context);
            Notifications     = new GenericRepository<Notification>(context);
            AuditLogs         = new GenericRepository<AuditLog>(context);
        }

        public async Task<int> SaveChangesAsync()
            => await _context.SaveChangesAsync();

        public void Dispose()
            => _context.Dispose();
    }
}
