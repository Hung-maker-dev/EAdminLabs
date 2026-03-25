using eAdmin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace eAdmin.Repository.Data
{
    /// <summary>
    /// EF Core DbContext – Database First approach.
    /// 
    /// Scaffold command (chạy từ thư mục eAdmin.Repository):
    ///   dotnet ef dbcontext scaffold "Server=.;Database=eAdminLabDB;Trusted_Connection=True;"
    ///     Microsoft.EntityFrameworkCore.SqlServer
    ///     -o ../eAdmin.Domain/Entities
    ///     --context AppDbContext --context-dir Data --force
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User>             Users             { get; set; }
        public DbSet<Role>             Roles             { get; set; }
        public DbSet<Department>       Departments       { get; set; }
        public DbSet<Lab>              Labs              { get; set; }
        public DbSet<Equipment>        Equipment         { get; set; }
        public DbSet<EquipmentType>    EquipmentTypes    { get; set; }
        public DbSet<Software>         Software          { get; set; }
        public DbSet<Complaint>        Complaints        { get; set; }
        public DbSet<ComplaintType>    ComplaintTypes    { get; set; }
        public DbSet<LabSchedule>      LabSchedules      { get; set; }
        public DbSet<ExtraLabRequest>  ExtraLabRequests  { get; set; }
        public DbSet<LearningMaterial> LearningMaterials { get; set; }
        public DbSet<Notification>     Notifications     { get; set; }
        public DbSet<AuditLog>         AuditLogs         { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ──────────────────────────────────────────────────────
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.UserId);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.HasOne(u => u.Role)
                 .WithMany(r => r.Users)
                 .HasForeignKey(u => u.RoleId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(u => u.Department)
                 .WithMany(d => d.Users)
                 .HasForeignKey(u => u.DepartmentId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Department ────────────────────────────────────────────────
            modelBuilder.Entity<Department>(e =>
            {
                e.HasKey(d => d.DepartmentId);
                e.Property(d => d.DepartmentName).IsRequired().HasMaxLength(100);
                e.Property(d => d.Code).HasMaxLength(20);
                e.HasIndex(d => d.DepartmentName).IsUnique();
                e.HasOne(d => d.HodUser)
                 .WithMany()
                 .HasForeignKey(d => d.HodUserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Equipment ─────────────────────────────────────────────────
            modelBuilder.Entity<Equipment>(e =>
            {
                e.HasKey(eq => eq.EquipmentId);
                e.HasIndex(eq => eq.AssetCode).IsUnique();
                e.Property(eq => eq.Condition)
                 .HasConversion<string>()
                 .HasDefaultValue("Good");
                e.HasOne(eq => eq.Lab)
                 .WithMany(l => l.Equipments)
                 .HasForeignKey(eq => eq.LabId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(eq => eq.EquipmentType)
                 .WithMany(et => et.Equipments)
                 .HasForeignKey(eq => eq.EquipmentTypeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Complaint ─────────────────────────────────────────────────
            modelBuilder.Entity<Complaint>(e =>
            {
                e.HasKey(c => c.ComplaintId);
                // Reporter – Restrict (không xóa user nếu còn complaint)
                e.HasOne(c => c.Reporter)
                 .WithMany(u => u.ReportedComplaints)
                 .HasForeignKey(c => c.ReportedBy)
                 .OnDelete(DeleteBehavior.Restrict);
                // Assignee – SetNull (nếu xóa user thì bỏ phân công)
                e.HasOne(c => c.Assignee)
                 .WithMany(u => u.AssignedComplaints)
                 .HasForeignKey(c => c.AssignedTo)
                 .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(c => c.Lab)
                 .WithMany(l => l.Complaints)
                 .HasForeignKey(c => c.LabId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(c => c.Equipment)
                 .WithMany(eq => eq.Complaints)
                 .HasForeignKey(c => c.EquipmentId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── LabSchedule ───────────────────────────────────────────────
            modelBuilder.Entity<LabSchedule>(e =>
            {
                e.HasKey(s => s.ScheduleId);
                e.HasOne(s => s.Lab)
                 .WithMany(l => l.LabSchedules)
                 .HasForeignKey(s => s.LabId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(s => s.Instructor)
                 .WithMany(u => u.LabSchedules)
                 .HasForeignKey(s => s.InstructorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ExtraLabRequest ───────────────────────────────────────────
            modelBuilder.Entity<ExtraLabRequest>(e =>
            {
                e.HasKey(r => r.RequestId);
                e.HasOne(r => r.Requester)
                 .WithMany()
                 .HasForeignKey(r => r.RequestedBy)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Lab)
                 .WithMany(l => l.ExtraLabRequests)
                 .HasForeignKey(r => r.LabId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── LearningMaterial ──────────────────────────────────────────
            modelBuilder.Entity<LearningMaterial>(e =>
            {
                e.HasKey(m => m.MaterialId);
                e.Property(m => m.Description).HasMaxLength(500);
                e.HasOne(m => m.Uploader)
                 .WithMany()
                 .HasForeignKey(m => m.UploadedBy)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(m => m.Department)
                 .WithMany(d => d.LearningMaterials)
                 .HasForeignKey(m => m.DepartmentId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Notification ──────────────────────────────────────────────
            modelBuilder.Entity<Notification>(e =>
            {
                e.HasKey(n => n.NotificationId);
                e.HasOne(n => n.Recipient)
                 .WithMany(u => u.Notifications)
                 .HasForeignKey(n => n.RecipientUserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── AuditLog ──────────────────────────────────────────────────
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.HasKey(a => a.LogId);
                e.HasOne(a => a.User)
                 .WithMany()
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Seed Roles ────────────────────────────────────────────────
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "Admin",      Description = "Quản trị toàn hệ thống" },
                new Role { RoleId = 2, RoleName = "HOD",        Description = "Trưởng khoa" },
                new Role { RoleId = 3, RoleName = "Instructor",  Description = "Giảng viên" },
                new Role { RoleId = 4, RoleName = "TechStaff",  Description = "Nhân viên kỹ thuật" },
                new Role { RoleId = 5, RoleName = "Student",    Description = "Sinh viên" }
            );

            // ── Seed EquipmentTypes ───────────────────────────────────────
            modelBuilder.Entity<EquipmentType>().HasData(
                new EquipmentType { EquipmentTypeId = 1, TypeName = "Computer" },
                new EquipmentType { EquipmentTypeId = 2, TypeName = "Printer" },
                new EquipmentType { EquipmentTypeId = 3, TypeName = "LCD" },
                new EquipmentType { EquipmentTypeId = 4, TypeName = "AC" },
                new EquipmentType { EquipmentTypeId = 5, TypeName = "DigitalBoard" }
            );

            // ── Seed ComplaintTypes ───────────────────────────────────────
            modelBuilder.Entity<ComplaintType>().HasData(
                new ComplaintType { ComplaintTypeId = 1, TypeName = "Hardware" },
                new ComplaintType { ComplaintTypeId = 2, TypeName = "Software" },
                new ComplaintType { ComplaintTypeId = 3, TypeName = "Network" },
                new ComplaintType { ComplaintTypeId = 4, TypeName = "Power" },
                new ComplaintType { ComplaintTypeId = 5, TypeName = "Other" }
            );
        }
    }
}
