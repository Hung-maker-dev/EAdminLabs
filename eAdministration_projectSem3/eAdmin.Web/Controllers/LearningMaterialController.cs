using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;
using eAdmin.Web.Filters;
using eAdmin.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace eAdmin.Web.Controllers
{
    [Authorize]
    public class LearningMaterialController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notify;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".zip", ".png", ".jpg", ".jpeg" };

        private static readonly string[] MaterialTypes =
            { "Syllabus", "InstallGuide", "ELearning", "ServerInfo", "Other" };

        public LearningMaterialController(IUnitOfWork uow, INotificationService notify, IWebHostEnvironment env)
        {
            _uow = uow;
            _notify = notify;
            _env = env;
        }

        // ── GET: /LearningMaterial ─────────────────────────────────────────
        public async Task<IActionResult> Index(string? type, int? deptId, string? search)
        {
            var role = User.FindFirst("Role")?.Value ?? "";
            var userDeptClaim = User.FindFirst("DepartmentId")?.Value;
            int.TryParse(userDeptClaim, out var userDept);

            var all = await _uow.LearningMaterials.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var depts = await _uow.Departments.GetAllAsync();

            // Students/ Instructor only see their dept + public
            if (role == "Student" || role == "Instructor")
            {
                all = all.Where(m =>
                    m.IsPublic ||
                    m.DepartmentId == null ||
                    m.DepartmentId == userDept
                );
            }

            if (!string.IsNullOrEmpty(type)) all = all.Where(m => m.MaterialType == type);
            if (deptId.HasValue) all = all.Where(m => m.DepartmentId == deptId);

            // ── Search filter ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                all = all.Where(m =>
                    m.Title.ToLower().Contains(searchLower) ||
                    (m.Description != null && m.Description.ToLower().Contains(searchLower)) ||
                    m.MaterialType.ToLower().Contains(searchLower));
            }

            var allList = all.ToList();

            // ── Build a set of (Title+Type+Dept) to detect cross-user duplicates ──
            // A material is a "duplicate" if another record with different UploadedBy
            // shares the same Title (case-insensitive) + MaterialType + DepartmentId
            var allMaterials = await _uow.LearningMaterials.GetAllAsync(); // full unfiltered set
            var vm = allList.Select(m =>
            {
                var hasCrossUserDuplicate = allMaterials.Any(other =>
                    other.MaterialId != m.MaterialId &&
                    other.Title.ToLower() == m.Title.ToLower() &&
                    other.MaterialType == m.MaterialType &&
                    other.DepartmentId == m.DepartmentId &&
                    other.UploadedBy != m.UploadedBy);

                return new LearningMaterialViewModel
                {
                    MaterialId = m.MaterialId,
                    Title = m.Title,
                    MaterialType = m.MaterialType,
                    Description = m.Description,
                    FilePath = m.FilePath,
                    ExternalUrl = m.ExternalUrl,
                    DepartmentId = m.DepartmentId,
                    IsPublic = m.IsPublic,
                    CreatedAt = m.CreatedAt,
                    UploadedBy = m.UploadedBy,
                    DeptName = depts.FirstOrDefault(d => d.DepartmentId == m.DepartmentId)?.DepartmentName ?? "All Departments",
                    UploaderName = users.FirstOrDefault(u => u.UserId == m.UploadedBy)?.FullName ?? "",
                    HasCrossUserDuplicate = hasCrossUserDuplicate
                };
            }).OrderByDescending(m => m.CreatedAt).ToList();

            ViewBag.Types = MaterialTypes;
            ViewBag.Departments = await _uow.Departments.GetAllAsync();
            ViewBag.FilterType = type;
            ViewBag.FilterDept = deptId;
            ViewBag.Search = search;
            return View(vm);
        }

        // ── GET: /LearningMaterial/Create ──────────────────────────────────
        [HttpGet]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Create()
        {
            await PopulateDepts();
            return View(new LearningMaterialViewModel { IsPublic = false });
        }

        // ── POST: /LearningMaterial/Create ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Create(LearningMaterialViewModel vm, IFormFile? uploadedFile)
        {
            // ── Input validation ───────────────────────────────────────────
            vm.Title = vm.Title?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();
            vm.ExternalUrl = vm.ExternalUrl?.Trim();

            if (string.IsNullOrEmpty(vm.Title))
                ModelState.AddModelError("Title", "Title is required.");
            else if (vm.Title.Length < 3)
                ModelState.AddModelError("Title", "Title must be at least 3 characters.");

            if (string.IsNullOrEmpty(vm.MaterialType) || !MaterialTypes.Contains(vm.MaterialType))
                ModelState.AddModelError("MaterialType", "Please select a valid material type.");

            bool hasFile = uploadedFile != null && uploadedFile.Length > 0;
            bool hasUrl = !string.IsNullOrEmpty(vm.ExternalUrl);
            if (!hasFile && !hasUrl)
                ModelState.AddModelError("", "Please either upload a file or provide an External URL.");

            if (hasUrl && !Uri.TryCreate(vm.ExternalUrl, UriKind.Absolute, out _))
                ModelState.AddModelError("ExternalUrl", "Please enter a valid URL (must start with http:// or https://).");

            if (hasFile)
            {
                var ext = Path.GetExtension(uploadedFile!.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    ModelState.AddModelError("", $"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

                if (uploadedFile.Length > 20 * 1024 * 1024)
                    ModelState.AddModelError("", "File size must not exceed 20MB.");
            }

            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // ── Duplicate check: SAME user + same Title + same Type + same Department → BLOCK ──
            var selfDuplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.UploadedBy == uid &&
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId);

            if (selfDuplicates.Any())
            {
                ModelState.AddModelError("Title",
                    $"Bạn đã tạo tài liệu '{vm.MaterialType}' với tiêu đề '{vm.Title}' cho khoa này rồi. " +
                    $"Vui lòng dùng tiêu đề khác.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDepts();
                return View(vm);
            }

            // ── Check cross-user duplicate (warn only, do NOT block) ───────
            var crossUserDuplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.UploadedBy != uid &&
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId);

            bool hasCrossUserWarning = crossUserDuplicates.Any();

            // ── Save file ──────────────────────────────────────────────────
            string? savedFilePath = null;
            if (hasFile)
                savedFilePath = await SaveFileAsync(uploadedFile!);

            await _uow.LearningMaterials.AddAsync(new LearningMaterial
            {
                Title = vm.Title,
                MaterialType = vm.MaterialType,
                Description = vm.Description,
                FilePath = savedFilePath,
                ExternalUrl = vm.ExternalUrl,
                DepartmentId = vm.DepartmentId,
                IsPublic = vm.IsPublic,
                UploadedBy = uid,
                CreatedAt = DateTime.UtcNow
            });
            await _uow.SaveChangesAsync();

            // Notify students in department
            var students = await _uow.Users.FindAsync(u =>
                u.Role.RoleName == "Student" &&
                u.IsActive &&
                (
                    vm.IsPublic ||                             // Public → tất cả student
                    (vm.DepartmentId.HasValue &&              // Theo khoa → đúng khoa
                     u.DepartmentId == vm.DepartmentId)
                )
            );

            foreach (var s in students)
            {
                await _notify.SendAsync(
                    s.UserId,
                    "InApp",
                    "New Learning Material",
                    $"New {vm.MaterialType} uploaded: {vm.Title}",
                    "LearningMaterial",
                    0 // nếu muốn chuẩn hơn có thể thay bằng materialId
                );
            }

            if (hasCrossUserWarning)
                TempData["Warning"] = $"Tài liệu '{vm.Title}' đã được tạo thành công, nhưng lưu ý: " +
                                      $"đã có giảng viên khác tạo tài liệu tương tự (cùng tiêu đề, loại và khoa).";
            else
                TempData["Success"] = $"Material '{vm.Title}' uploaded successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ── GET: /LearningMaterial/Edit/5 ──────────────────────────────────
        [HttpGet]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Edit(int id)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = User.FindFirst("Role")?.Value ?? "";
            var m = await _uow.LearningMaterials.GetByIdAsync(id);

            if (m == null) return NotFound();
            if (role != "Admin" && m.UploadedBy != uid)
            {
                TempData["Error"] = "You can only edit materials you uploaded.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDepts();
            return View(new LearningMaterialViewModel
            {
                MaterialId = m.MaterialId,
                Title = m.Title,
                MaterialType = m.MaterialType,
                Description = m.Description,
                FilePath = m.FilePath,
                ExternalUrl = m.ExternalUrl,
                DepartmentId = m.DepartmentId,
                IsPublic = m.IsPublic,
                UploadedBy = m.UploadedBy
            });
        }

        // ── POST: /LearningMaterial/Edit ───────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Edit(LearningMaterialViewModel vm, IFormFile? uploadedFile)
        {
            vm.Title = vm.Title?.Trim() ?? string.Empty;
            vm.Description = vm.Description?.Trim();
            vm.ExternalUrl = vm.ExternalUrl?.Trim();

            if (string.IsNullOrEmpty(vm.Title))
                ModelState.AddModelError("Title", "Title is required.");
            else if (vm.Title.Length < 3)
                ModelState.AddModelError("Title", "Title must be at least 3 characters.");

            if (string.IsNullOrEmpty(vm.MaterialType) || !MaterialTypes.Contains(vm.MaterialType))
                ModelState.AddModelError("MaterialType", "Please select a valid material type.");

            bool hasNewFile = uploadedFile != null && uploadedFile.Length > 0;
            bool hasUrl = !string.IsNullOrEmpty(vm.ExternalUrl);
            bool hasExistingFile = !string.IsNullOrEmpty(vm.FilePath);

            if (!hasNewFile && !hasUrl && !hasExistingFile)
                ModelState.AddModelError("", "Please upload a file or provide an External URL.");

            if (hasUrl && !Uri.TryCreate(vm.ExternalUrl, UriKind.Absolute, out _))
                ModelState.AddModelError("ExternalUrl", "Please enter a valid URL (must start with http:// or https://).");

            if (hasNewFile)
            {
                var ext = Path.GetExtension(uploadedFile!.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    ModelState.AddModelError("", $"File type '{ext}' is not allowed.");

                if (uploadedFile.Length > 20 * 1024 * 1024)
                    ModelState.AddModelError("", "File size must not exceed 20MB.");
            }

            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = User.FindFirst("Role")?.Value ?? "";

            // ── Duplicate check: same owner, same title+type+dept, exclude self → BLOCK ──
            var selfDuplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.UploadedBy == uid &&
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId &&
                m.MaterialId != vm.MaterialId);

            if (selfDuplicates.Any())
                ModelState.AddModelError("Title",
                    $"Bạn đã có tài liệu '{vm.MaterialType}' với tiêu đề '{vm.Title}' trong khoa này rồi.");

            // Admin: also block if exact duplicate exists from any owner (optional stricter rule for admin edits)
            // Keeping consistent: Admin edits also only blocked if same-owner duplicate

            if (!ModelState.IsValid)
            {
                await PopulateDepts();
                return View(vm);
            }

            // ── Cross-user duplicate warning (after save) ──────────────────
            var crossUserDuplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.UploadedBy != uid &&
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId &&
                m.MaterialId != vm.MaterialId);

            bool hasCrossUserWarning = crossUserDuplicates.Any();

            var entity = await _uow.LearningMaterials.GetByIdAsync(vm.MaterialId);
            if (entity == null) return NotFound();

            // Replace file if new one uploaded
            if (hasNewFile)
            {
                if (!string.IsNullOrEmpty(entity.FilePath))
                    DeletePhysicalFile(entity.FilePath);
                entity.FilePath = await SaveFileAsync(uploadedFile!);
            }
            else if (!hasExistingFile)
            {
                if (!string.IsNullOrEmpty(entity.FilePath))
                    DeletePhysicalFile(entity.FilePath);
                entity.FilePath = null;
            }

            entity.Title = vm.Title;
            entity.MaterialType = vm.MaterialType;
            entity.Description = vm.Description;
            entity.ExternalUrl = vm.ExternalUrl;
            entity.DepartmentId = vm.DepartmentId;
            entity.IsPublic = vm.IsPublic;

            _uow.LearningMaterials.Update(entity);
            await _uow.SaveChangesAsync();

            if (hasCrossUserWarning)
                TempData["Warning"] = $"Tài liệu '{vm.Title}' đã được cập nhật, nhưng lưu ý: " +
                                      $"đã có giảng viên khác tạo tài liệu tương tự (cùng tiêu đề, loại và khoa).";
            else
                TempData["Success"] = $"Material '{vm.Title}' updated.";

            return RedirectToAction(nameof(Index));
        }

        // ── POST: /LearningMaterial/Delete ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles("Admin", "Instructor")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = User.FindFirst("Role")?.Value ?? "";
            var m = await _uow.LearningMaterials.GetByIdAsync(id);

            if (m == null)
            {
                TempData["Error"] = "Material not found.";
                return RedirectToAction(nameof(Index));
            }

            if (role != "Admin" && m.UploadedBy != uid)
            {
                TempData["Error"] = "You can only delete materials you uploaded.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Delete physical file
            if (!string.IsNullOrEmpty(m.FilePath))
                DeletePhysicalFile(m.FilePath);

            // 2. Delete related notifications
            var relatedNotifs = await _uow.Notifications.FindAsync(
                n => n.RelatedEntityType == "LearningMaterial" &&
                     n.RelatedEntityId == m.MaterialId);
            foreach (var n in relatedNotifs)
                _uow.Notifications.Remove(n);

            // 3. Delete record
            _uow.LearningMaterials.Remove(m);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Material '{m.Title}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private async Task<string> SaveFileAsync(IFormFile file)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "materials");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(file.FileName)}";
            var fullPath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/materials/{fileName}";
        }

        private void DeletePhysicalFile(string filePath)
        {
            var fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        private async Task PopulateDepts()
        {
            ViewBag.Departments = new SelectList(
                await _uow.Departments.GetAllAsync(), "DepartmentId", "DepartmentName");
            ViewBag.Types = MaterialTypes;
        }
    }
}