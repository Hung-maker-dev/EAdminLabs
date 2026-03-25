-- ============================================================
-- eAdministration of Computer Labs
-- Database Setup Script – SQL Server 2019+
-- ============================================================

CREATE DATABASE eAdminLabDB;
GO
USE eAdminLabDB;
GO

-- Roles
CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50)  NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL
);
INSERT INTO Roles (RoleName, Description) VALUES
    (N'Admin',      N'Quản trị toàn hệ thống'),
    (N'HOD',        N'Trưởng khoa'),
    (N'Instructor', N'Giảng viên'),
    (N'TechStaff',  N'Nhân viên kỹ thuật'),
    (N'Student',    N'Sinh viên');

-- Departments
CREATE TABLE Departments (
    DepartmentId INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentName NVARCHAR(100) NOT NULL UNIQUE,
    Code           NVARCHAR(20) NULL,
    HodUserId    INT NULL,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);

-- Users
CREATE TABLE Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(100) NOT NULL UNIQUE,
    Phone        NVARCHAR(20)  NULL,
    RoleId       INT NOT NULL REFERENCES Roles(RoleId),
    DepartmentId INT NULL REFERENCES Departments(DepartmentId),
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);
ALTER TABLE Departments
    ADD CONSTRAINT FK_Dept_Hod FOREIGN KEY (HodUserId) REFERENCES Users(UserId);

-- Labs
CREATE TABLE Labs (
    LabId       INT IDENTITY(1,1) PRIMARY KEY,
    LabName     NVARCHAR(100) NOT NULL UNIQUE,
    Location    NVARCHAR(200) NULL,
    Capacity    INT NOT NULL DEFAULT 0,
    IsActive    BIT NOT NULL DEFAULT 1,
    Description NVARCHAR(500) NULL
);

-- EquipmentTypes
CREATE TABLE EquipmentTypes (
    EquipmentTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeName        NVARCHAR(50) NOT NULL UNIQUE,
    Description     NVARCHAR(255) NULL
);
INSERT INTO EquipmentTypes(TypeName) VALUES
    (N'Computer'),(N'Printer'),(N'LCD'),(N'AC'),(N'DigitalBoard');

-- Equipment
CREATE TABLE Equipment (
    EquipmentId     INT IDENTITY(1,1) PRIMARY KEY,
    LabId           INT NOT NULL REFERENCES Labs(LabId),
    EquipmentTypeId INT NOT NULL REFERENCES EquipmentTypes(EquipmentTypeId),
    AssetCode       NVARCHAR(50)  NOT NULL UNIQUE,
    Model           NVARCHAR(100) NULL,
    SerialNumber    NVARCHAR(100) NULL UNIQUE,
    PurchaseDate    DATE NULL,
    WarrantyExpiry  DATE NULL,
    Condition       NVARCHAR(20) NOT NULL DEFAULT 'Good'
        CHECK(Condition IN ('Good','Fair','Poor','OutOfService')),
    Notes           NVARCHAR(500) NULL
);

-- Software
CREATE TABLE Software (
    SoftwareId         INT IDENTITY(1,1) PRIMARY KEY,
    SoftwareName       NVARCHAR(100) NOT NULL,
    Version            NVARCHAR(50)  NULL,
    LicenseKey         NVARCHAR(200) NULL,
    LicenseExpiry      DATE NULL,
    InstallGuideUrl    NVARCHAR(500) NULL,
    LabId              INT NULL REFERENCES Labs(LabId),
    IsNotificationSent BIT NOT NULL DEFAULT 0,
    Notes              NVARCHAR(500) NULL
);

-- ComplaintTypes
CREATE TABLE ComplaintTypes (
    ComplaintTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeName        NVARCHAR(100) NOT NULL UNIQUE,
    Description     NVARCHAR(255) NULL
);
INSERT INTO ComplaintTypes(TypeName) VALUES
    (N'Hardware'),(N'Software'),(N'Network'),(N'Power'),(N'Other');

-- Complaints
CREATE TABLE Complaints (
    ComplaintId     INT IDENTITY(1,1) PRIMARY KEY,
    LabId           INT NOT NULL REFERENCES Labs(LabId),
    EquipmentId     INT NULL  REFERENCES Equipment(EquipmentId),
    ReportedBy      INT NOT NULL REFERENCES Users(UserId),
    AssignedTo      INT NULL  REFERENCES Users(UserId),
    ComplaintTypeId INT NOT NULL REFERENCES ComplaintTypes(ComplaintTypeId),
    Title           NVARCHAR(200)  NOT NULL,
    Description     NVARCHAR(1000) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK(Status IN ('Pending','Assigned','InProgress','Resolved','Closed')),
    Priority        NVARCHAR(10) NOT NULL DEFAULT 'Medium'
        CHECK(Priority IN ('Low','Medium','High','Critical')),
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    ResolvedAt      DATETIME NULL
);

-- LabSchedules
CREATE TABLE LabSchedules (
    ScheduleId    INT IDENTITY(1,1) PRIMARY KEY,
    LabId         INT NOT NULL REFERENCES Labs(LabId),
    InstructorId  INT NOT NULL REFERENCES Users(UserId),
    SubjectName   NVARCHAR(100) NOT NULL,
    DayOfWeek     INT NOT NULL CHECK(DayOfWeek BETWEEN 1 AND 7),
    StartTime     TIME NOT NULL,
    EndTime       TIME NOT NULL,
    EffectiveFrom DATETIME NOT NULL,
    EffectiveTo   DATETIME NULL,
    IsActive      BIT NOT NULL DEFAULT 1
);

-- ExtraLabRequests
CREATE TABLE ExtraLabRequests (
    RequestId   INT IDENTITY(1,1) PRIMARY KEY,
    RequestedBy INT NOT NULL REFERENCES Users(UserId),
    LabId       INT NULL REFERENCES Labs(LabId),
    RequestDate DATE NOT NULL,
    StartTime   TIME NOT NULL,
    EndTime     TIME NOT NULL,
    Purpose     NVARCHAR(500) NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending'
        CHECK(Status IN ('Pending','Approved','Rejected')),
    AdminReply  NVARCHAR(500) NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT GETDATE()
);

-- LearningMaterials
CREATE TABLE LearningMaterials (
    MaterialId   INT IDENTITY(1,1) PRIMARY KEY,
    Title        NVARCHAR(200) NOT NULL,
    MaterialType NVARCHAR(50) NOT NULL
        CHECK(MaterialType IN ('Syllabus','InstallGuide','ELearning','ServerInfo','Other')),
    Description  NVARCHAR(500) NULL,
    FilePath     NVARCHAR(500) NULL,
    ExternalUrl  NVARCHAR(500) NULL,
    DepartmentId INT NULL REFERENCES Departments(DepartmentId),
    UploadedBy   INT NOT NULL REFERENCES Users(UserId),
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE(),
    IsPublic     BIT NOT NULL DEFAULT 1
);

-- Notifications
CREATE TABLE Notifications (
    NotificationId    INT IDENTITY(1,1) PRIMARY KEY,
    RecipientUserId   INT NOT NULL REFERENCES Users(UserId),
    NotificationType  NVARCHAR(30) NOT NULL
        CHECK(NotificationType IN ('SMS','InApp')),
    Subject           NVARCHAR(200) NULL,
    Message           NVARCHAR(1000) NOT NULL,
    RelatedEntityType NVARCHAR(50) NULL,
    RelatedEntityId   INT NULL,
    IsSent            BIT NOT NULL DEFAULT 0,
    IsRead            BIT NOT NULL DEFAULT 0,
    SentAt            DATETIME NULL,
    CreatedAt         DATETIME NOT NULL DEFAULT GETDATE()
);

-- AuditLogs
CREATE TABLE AuditLogs (
    LogId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId     INT NULL REFERENCES Users(UserId),
    Action     NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(50)  NULL,
    EntityId   INT NULL,
    Details    NVARCHAR(2000) NULL,
    CreatedAt  DATETIME NOT NULL DEFAULT GETDATE()
);

-- ============================================================
-- SEED DATA – Tài khoản mặc định (password = Admin@123)
-- PasswordHash = SHA256("Admin@123" + "eAdminLabSalt#2024")
-- ============================================================
INSERT INTO Departments(DepartmentName) VALUES
    (N'Công Nghệ Thông Tin'),
    (N'Điện Tử Viễn Thông'),
    (N'Kỹ Thuật Phần Mềm');

INSERT INTO Users(Username, PasswordHash, FullName, Email, Phone, RoleId, DepartmentId, IsActive)
VALUES
    ('admin',    '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918', N'System Administrator', 'admin@lab.edu.vn',      '0900000001', 1, NULL, 1),
    ('hod_cntt', 'W6ph5Mm5Pz8GgiULbPgzG37mj9g558SfRz/iXWqBNpk=',                       N'Trưởng Khoa CNTT',     'hod.cntt@lab.edu.vn',   '0900000002', 2, 1,    1),
    ('gv01',     'W6ph5Mm5Pz8GgiULbPgzG37mj9g558SfRz/iXWqBNpk=',                       N'Nguyễn Văn A',         'gv01@lab.edu.vn',       '0900000003', 3, 1,    1),
    ('tech01',   'W6ph5Mm5Pz8GgiULbPgzG37mj9g558SfRz/iXWqBNpk=',                       N'Lê Văn Kỹ Thuật',      'tech01@lab.edu.vn',     '0900000004', 4, 1,    1),
    ('sv001',    'W6ph5Mm5Pz8GgiULbPgzG37mj9g558SfRz/iXWqBNpk=',                       N'Trần Thị Sinh Viên',   'sv001@lab.edu.vn',      NULL,         5, 1,    1);

INSERT INTO Labs(LabName, Location, Capacity) VALUES
    (N'Lab A01', N'Tòa A - Tầng 1', 40),
    (N'Lab A02', N'Tòa A - Tầng 1', 40),
    (N'Lab B01', N'Tòa B - Tầng 2', 35);
GO

PRINT 'Database eAdminLabDB created successfully!';


USE eAdminLabDB;

-- admin / Admin@123
UPDATE Users SET PasswordHash = 'GVs9DgaXyNb0iCO34ZPStyzInGOgTLXzAExWlXiS3/A='
WHERE Username = 'admin';

-- hod_cntt / Hod@123
UPDATE Users SET PasswordHash = 'zDMndo+p0i4vDFQoPZH0DgNgETFyuO/dp6DtCQ7oyjY='
WHERE Username = 'hod_cntt';

-- gv01 / Gv@123
UPDATE Users SET PasswordHash = '3/89JFv7w+wQUKzaXgqmPXkCXWbn/nuxFedEPxlztd8='
WHERE Username = 'gv01';

-- tech01 / Tech@123
UPDATE Users SET PasswordHash = 'POdWuVfAbwoSUrbgvf4Ndo8oxBwxLYZpkQTre3ck+4Y='
WHERE Username = 'tech01';

-- sv001 / Sv@123
UPDATE Users SET PasswordHash = 'KTHGQ3lc2UFdsHNcMwOiZmSH1cevH3y5q6AG05iOkSE='
WHERE Username = 'sv001';