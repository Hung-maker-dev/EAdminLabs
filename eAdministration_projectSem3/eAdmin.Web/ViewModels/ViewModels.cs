using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace eAdmin.Web.ViewModels
{
    public class LoginViewModel
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required][DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public class UserViewModel
    {
        public int UserId { get; set; }
        [Required][StringLength(50)] public string Username { get; set; } = string.Empty;
        [Required][StringLength(100)] public string FullName { get; set; } = string.Empty;
        [Required][EmailAddress] public string Email { get; set; } = string.Empty;
        [Phone] public string? Phone { get; set; }
        [Required] public int RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public bool IsActive { get; set; } = true;
        [StringLength(100, MinimumLength = 6)] public string? Password { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string DeptName { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [Required][DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
        [Required][StringLength(100, MinimumLength = 6)][DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
        [Required][DataType(DataType.Password)][Compare("NewPassword")] public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RoleViewModel
    {
        public int RoleId { get; set; }
        [Required][StringLength(50)] public string RoleName { get; set; } = string.Empty;
        [StringLength(200)] public string? Description { get; set; }
        public int UserCount { get; set; }
    }

    public class DepartmentViewModel
    {
        public int DepartmentId { get; set; }
        [Required][StringLength(100)] public string DepartmentName { get; set; } = string.Empty;
        [StringLength(20)] public string? Code { get; set; }
        public int? HodUserId { get; set; }
        public string HodName { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }

    public class ComplaintCreateViewModel
    {
        [Required][StringLength(200)] public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required] public int LabId { get; set; }
        public int? EquipmentId { get; set; }
        [Required] public int ComplaintTypeId { get; set; }
        [Required] public string Priority { get; set; } = "Medium";
    }

    public class ComplaintListViewModel
    {
        public int ComplaintId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public string ReporterName { get; set; } = string.Empty;
        public string? AssigneeName { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class ComplaintTypeViewModel
    {
        public int ComplaintTypeId { get; set; }
        [Required][StringLength(100)] public string TypeName { get; set; } = string.Empty;
        [StringLength(200)] public string? Description { get; set; }
        public int ComplaintCount { get; set; }
    }

    public class ScheduleCreateViewModel
    {
        [Required] public int LabId { get; set; }
        [Required] public int InstructorId { get; set; }
        [Required][StringLength(100)] public string SubjectName { get; set; } = string.Empty;
        [Required][Range(1, 7)] public int DayOfWeek { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }
        [Required] public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public int ScheduleId { get; set; }
    }

    public class ExtraLabRequestViewModel
    {
        public int RequestId { get; set; }
        public int? LabId { get; set; }
        [Required] public DateTime RequestDate { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }
        [StringLength(500)] public string? Purpose { get; set; }
        public string? AdminReply { get; set; }
        public string Status { get; set; } = "Pending";
        public string RequesterName { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
    }

    public class EquipmentTypeViewModel
    {
        public int EquipmentTypeId { get; set; }
        [Required][StringLength(100)] public string TypeName { get; set; } = string.Empty;
        [StringLength(200)] public string? Description { get; set; }
        public int EquipmentCount { get; set; }
    }

    public class EquipmentViewModel : IValidatableObject
    {
        public int EquipmentId { get; set; }

        [Required(ErrorMessage = "Asset Code is required")]
        [StringLength(50)]
        public string AssetCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lab is required")]
        public int? LabId { get; set; }

        [Required(ErrorMessage = "Equipment Type is required")]
        public int? EquipmentTypeId { get; set; }

        [Required(ErrorMessage = "Model is required")]
        public string? Model { get; set; }

        [Required(ErrorMessage = "Serial Number is required")]
        public string? SerialNumber { get; set; }

        [Required(ErrorMessage = "Purchase Date is required")]
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        [Required(ErrorMessage = "Warranty Expiry is required")]
        [DataType(DataType.Date)]
        public DateTime? WarrantyExpiry { get; set; }

        [Required]
        public string Condition { get; set; } = "Good";

        public string? Notes { get; set; }

        public string LabName { get; set; } = "";
        public string TypeName { get; set; } = "";

     
        public bool WarrantyExpired => WarrantyExpiry.HasValue && WarrantyExpiry.Value.Date < DateTime.Today;
        public bool WarrantyExpiringSoon => WarrantyExpiry.HasValue && !WarrantyExpired && WarrantyExpiry.Value.Date <= DateTime.Today.AddDays(30);

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PurchaseDate.HasValue && WarrantyExpiry.HasValue)
            {
                if (WarrantyExpiry.Value <= PurchaseDate.Value)
                    yield return new ValidationResult(
                        "Warranty must be after Purchase Date",
                        new[] { nameof(WarrantyExpiry) });

                if (PurchaseDate.Value > DateTime.Today)
                    yield return new ValidationResult(
                        "Purchase Date cannot be in the future",
                        new[] { nameof(PurchaseDate) });
            }
        }
    }

    public class SoftwareViewModel
    {
        public int SoftwareId { get; set; }
        [Required][StringLength(100)] public string SoftwareName { get; set; } = string.Empty;
        [StringLength(50)] public string? Version { get; set; }
        [StringLength(200)] public string? LicenseKey { get; set; }
        public DateTime? LicenseExpiry { get; set; }
        [StringLength(500)] public string? InstallGuideUrl { get; set; }
        public int? LabId { get; set; }
        public string LabName { get; set; } = string.Empty;
        public bool IsNotificationSent { get; set; }
        public bool IsExpired => LicenseExpiry.HasValue && LicenseExpiry.Value.Date < DateTime.Today;
        public bool IsExpiringSoon => LicenseExpiry.HasValue && !IsExpired && LicenseExpiry.Value.Date <= DateTime.Today.AddDays(30);
        public int? DaysUntilExpiry => LicenseExpiry.HasValue ? (int?)(LicenseExpiry.Value.Date - DateTime.Today).TotalDays : null;
    }

    public class LearningMaterialViewModel
    {
        public int MaterialId { get; set; }
        [Required][StringLength(200)] public string Title { get; set; } = string.Empty;
        [Required] public string MaterialType { get; set; } = "ELearning";
        [StringLength(500)] public string? Description { get; set; }
        public string? FilePath { get; set; }
        [StringLength(500)] public string? ExternalUrl { get; set; }
        public int? DepartmentId { get; set; }
        public bool IsPublic { get; set; }
        public string DeptName { get; set; } = string.Empty;
        public string UploaderName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int UploadedBy { get; set; }
        public bool HasCrossUserDuplicate { get; set; }
    }

    public class NotificationViewModel
    {
        public int NotificationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public bool IsSent { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
    }

    public class AuditLogViewModel
    {
        public int AuditLogId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogFilterViewModel
    {
        public string? Username { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<AuditLogViewModel> Results { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

    public class DashboardViewModel
    {
        public int TotalLabs { get; set; }
        public int TotalEquipments { get; set; }
        public int OpenComplaints { get; set; }
        public int PendingRequests { get; set; }
        public int ExpiringSoftwareCount { get; set; }
        public int UnreadNotifications { get; set; }
        public List<ComplaintListViewModel> RecentComplaints { get; set; } = new();
        public List<NotificationViewModel> RecentNotifications { get; set; } = new();
    }

    public class ReportFilterViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? LabId { get; set; }
        public string? Status { get; set; }
        public string? Condition { get; set; }
    }
}