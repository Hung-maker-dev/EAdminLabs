# e-Administration of Computer Labs

ASP.NET Core MVC (.NET 8) | Entity Framework Core 8 (Database First) | SQL Server | Onion Architecture

## Features Implemented

| Module                                                      | Controller                      | Status |
| ----------------------------------------------------------- | ------------------------------- | ------ |
| Login / Logout / Session                                    | AccountController               | ✅     |
| Manage Users (CRUD + Profile + Change Password)             | UserController                  | ✅     |
| Manage Roles (CRUD)                                         | RoleController                  | ✅     |
| Manage Departments (CRUD)                                   | DepartmentController            | ✅     |
| Manage Labs + Toggle Active                                 | LabController                   | ✅     |
| Lab Schedule (conflict detection, notify instructor)        | ScheduleController              | ✅     |
| Extra Lab Request (HOD → Admin, notifications)              | LabController                   | ✅     |
| Manage Equipment Types (CRUD)                               | EquipmentTypeController         | ✅     |
| Manage Equipment (CRUD + warranty alert)                    | EquipmentController             | ✅     |
| Manage Software (CRUD + expiry colors)                      | SoftwareController              | ✅     |
| SW Expiry Background Service (daily 08:00)                  | SoftwareExpiryBackgroundService | ✅     |
| e-Learning Materials Portal (upload/view/download)          | LearningMaterialController      | ✅     |
| Manage Complaint Types (CRUD)                               | ComplaintTypeController         | ✅     |
| Submit & Manage Complaints (auto-assign least-loaded)       | ComplaintController             | ✅     |
| Notification System (InApp + SMS, Bell icon)                | NotificationController          | ✅     |
| Manage Reports (Equipment, Complaints, SW Expiry, Overview) | ReportController                | ✅     |
| Audit Log (filter by user/action/entity/date)               | AuditLogController              | ✅     |

## Notification Triggers

| Event                                | Recipients      | Channel     |
| ------------------------------------ | --------------- | ----------- |
| Complaint auto-assigned              | TechStaff       | InApp + SMS |
| Complaint resolved/closed            | Reporter        | InApp       |
| Schedule created                     | Instructor      | InApp + SMS |
| Schedule updated                     | Instructor      | InApp + SMS |
| Schedule cancelled                   | Instructor      | InApp       |
| Extra Lab Request submitted          | All Admins      | InApp       |
| Extra Lab Request approved/rejected  | HOD             | InApp + SMS |
| New Learning Material uploaded       | Students (dept) | InApp       |
| Software license expiring (≤30 days) | All Admins      | InApp + SMS |

## Setup

1. Run `DatabaseSetup.sql` on SQL Server
2. Update connection string in `appsettings.json`
3. Run: `dotnet run --project eAdmin.Web`
4. Login:
   admin / Admin@123
   hod_cntt / Hod@123
   gv01 / Gv@123
   tech01 / Tech@123
   sv001 / Sv@123

## Architecture

```
eAdmin.Domain       → Entities, Interfaces
eAdmin.Repository   → EF Core DbContext, GenericRepository, UnitOfWork
eAdmin.Service      → Business Logic Services
eAdmin.Web          → MVC Controllers, Views, Filters, Background Services
```
