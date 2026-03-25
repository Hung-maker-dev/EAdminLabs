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
        public async Task<IActionResult> Index(string? type, int? deptId)
        {
            var role = User.FindFirst("Role")?.Value ?? "";
            var userDeptClaim = User.FindFirst("DepartmentId")?.Value;
            int.TryParse(userDeptClaim, out var userDept);

            var all = await _uow.LearningMaterials.GetAllAsync();
            var users = await _uow.Users.GetAllAsync();
            var depts = await _uow.Departments.GetAllAsync();

            // Students only see their dept + public
            if (role == "Student")
                all = all.Where(m => m.IsPublic || m.DepartmentId == userDept);

            if (!string.IsNullOrEmpty(type)) all = all.Where(m => m.MaterialType == type);
            if (deptId.HasValue) all = all.Where(m => m.DepartmentId == deptId);

            var vm = all.Select(m => new LearningMaterialViewModel
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
                UploaderName = users.FirstOrDefault(u => u.UserId == m.UploadedBy)?.FullName ?? ""
            }).OrderByDescending(m => m.CreatedAt).ToList();

            ViewBag.Types = MaterialTypes;
            ViewBag.Departments = await _uow.Departments.GetAllAsync();
            ViewBag.FilterType = type;
            ViewBag.FilterDept = deptId;
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

            // Must have at least one of: file, URL
            bool hasFile = uploadedFile != null && uploadedFile.Length > 0;
            bool hasUrl = !string.IsNullOrEmpty(vm.ExternalUrl);
            if (!hasFile && !hasUrl)
                ModelState.AddModelError("", "Please either upload a file or provide an External URL.");

            // Validate URL format if provided
            if (hasUrl && !Uri.TryCreate(vm.ExternalUrl, UriKind.Absolute, out _))
                ModelState.AddModelError("ExternalUrl", "Please enter a valid URL (must start with http:// or https://).");

            // Validate file extension
            if (hasFile)
            {
                var ext = Path.GetExtension(uploadedFile!.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    ModelState.AddModelError("", $"File type '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

                if (uploadedFile.Length > 20 * 1024 * 1024) // 20MB limit
                    ModelState.AddModelError("", "File size must not exceed 20MB.");
            }

            // ── Duplicate check: same Title + same Type + same Department ──
            var duplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId);

            if (duplicates.Any())
                ModelState.AddModelError("Title",
                    $"A '{vm.MaterialType}' material titled '{vm.Title}' already exists " +
                    $"for this department. Please use a different title or type.");

            if (!ModelState.IsValid)
            {
                await PopulateDepts();
                return View(vm);
            }

            // ── Save file ──────────────────────────────────────────────────
            string? savedFilePath = null;
            if (hasFile)
                savedFilePath = await SaveFileAsync(uploadedFile!);

            var uid = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

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
            if (vm.DepartmentId.HasValue)
            {
                var students = await _uow.Users.FindAsync(
                    u => u.Role.RoleName == "Student" &&
                         u.DepartmentId == vm.DepartmentId &&
                         u.IsActive);
                foreach (var s in students)
                    await _notify.SendAsync(s.UserId, "InApp",
                        "New Learning Material",
                        $"New {vm.MaterialType} uploaded: {vm.Title}",
                        "LearningMaterial", 0);
            }

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

            // Must have at least one source
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

            // Duplicate check — exclude self
            var duplicates = await _uow.LearningMaterials.FindAsync(m =>
                m.Title.ToLower() == vm.Title.ToLower() &&
                m.MaterialType == vm.MaterialType &&
                m.DepartmentId == vm.DepartmentId &&
                m.MaterialId != vm.MaterialId);

            if (duplicates.Any())
                ModelState.AddModelError("Title",
                    $"Another '{vm.MaterialType}' material titled '{vm.Title}' already exists for this department.");

            if (!ModelState.IsValid)
            {
                await PopulateDepts();
                return View(vm);
            }

            var m = await _uow.LearningMaterials.GetByIdAsync(vm.MaterialId);
            if (m == null) return NotFound();

            // Replace file if new one uploaded
            if (hasNewFile)
            {
                // Delete old file
                if (!string.IsNullOrEmpty(m.FilePath))
                    DeletePhysicalFile(m.FilePath);

                m.FilePath = await SaveFileAsync(uploadedFile!);
            }
            else if (!hasExistingFile)
            {
                // User cleared the file path manually
                if (!string.IsNullOrEmpty(m.FilePath))
                    DeletePhysicalFile(m.FilePath);
                m.FilePath = null;
            }

            m.Title = vm.Title;
            m.MaterialType = vm.MaterialType;
            m.Description = vm.Description;
            m.ExternalUrl = vm.ExternalUrl;
            m.DepartmentId = vm.DepartmentId;
            m.IsPublic = vm.IsPublic;

            _uow.LearningMaterials.Update(m);
            await _uow.SaveChangesAsync();

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