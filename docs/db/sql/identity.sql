IF OBJECT_ID(N'[identity].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
    CREATE TABLE [identity].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA [identity];');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[AppUser] (
        [Id] uniqueidentifier NOT NULL,
        [UserName] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [EmailConfirmed] bit NOT NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AppUser] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[Patient] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [DateOfBirth] date NOT NULL,
        [Gender] nvarchar(32) NOT NULL,
        [NationalId] nvarchar(20) NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Patient] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Patient_Gender] CHECK ([Gender] IN (N'Male', N'Female', N'Other'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[Room] (
        [Id] uniqueidentifier NOT NULL,
        [RoomNumber] nvarchar(50) NOT NULL,
        [RoomName] nvarchar(200) NOT NULL,
        [RoomType] nvarchar(32) NOT NULL,
        [DepartmentName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Room] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Room_RoomType] CHECK ([RoomType] IN (N'Clinical', N'Laboratory', N'XRay', N'Ultrasound', N'Endoscopy'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[Staff] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] uniqueidentifier NOT NULL,
        [StaffCode] nvarchar(50) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [StaffType] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Staff_StaffType] CHECK ([StaffType] IN (N'Doctor', N'Nurse', N'Receptionist', N'Technician')),
        CONSTRAINT [FK_Staff_AppUser_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [identity].[AppUser] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[PatientRepresentative] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [Relationship] nvarchar(32) NOT NULL,
        [AuthorizationProofDoc] nvarchar(1024) NULL,
        [VerificationStatus] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PatientRepresentative] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PatientRepresentative_Relationship] CHECK ([Relationship] IN (N'Self', N'Child', N'Parent', N'Spouse', N'Other')),
        CONSTRAINT [CK_PatientRepresentative_VerificationStatus] CHECK ([VerificationStatus] IN (N'Pending', N'Verified', N'Rejected', N'Revoked')),
        CONSTRAINT [FK_PatientRepresentative_AppUser_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [identity].[AppUser] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRepresentative_Patient_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [identity].[Patient] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[RoomAccount] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [AccountCode] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RoomAccount] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomAccount_AppUser_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [identity].[AppUser] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RoomAccount_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [identity].[Room] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[Doctor] (
        [Id] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [SpecialtyId] uniqueidentifier NOT NULL,
        [DoctorRankId] uniqueidentifier NOT NULL,
        [Biography] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Doctor] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Doctor_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [identity].[Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[StaffShiftAssignment] (
        [Id] uniqueidentifier NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [ShiftDate] date NOT NULL,
        [ShiftName] nvarchar(32) NOT NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NOT NULL,
        [IsShiftLeader] bit NOT NULL,
        CONSTRAINT [PK_StaffShiftAssignment] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StaffShiftAssignment_ShiftName] CHECK ([ShiftName] IN (N'Morning', N'Afternoon', N'FullDay')),
        CONSTRAINT [FK_StaffShiftAssignment_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [identity].[Room] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffShiftAssignment_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [identity].[Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [identity].[RoomDeviceSession] (
        [Id] uniqueidentifier NOT NULL,
        [RoomAccountId] uniqueidentifier NOT NULL,
        [DeviceId] nvarchar(128) NOT NULL,
        [DeviceIpAddress] nvarchar(45) NOT NULL,
        [SessionToken] nvarchar(64) NOT NULL,
        [LoginAtUtc] datetime2 NOT NULL,
        [LogoutAtUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_RoomDeviceSession] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomDeviceSession_RoomAccount_RoomAccountId] FOREIGN KEY ([RoomAccountId]) REFERENCES [identity].[RoomAccount] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AppUser_Email] ON [identity].[AppUser] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AppUser_PhoneNumber] ON [identity].[AppUser] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUser_UserName] ON [identity].[AppUser] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Doctor_DoctorRankId] ON [identity].[Doctor] ([DoctorRankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Doctor_SpecialtyId] ON [identity].[Doctor] ([SpecialtyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Doctor_StaffId] ON [identity].[Doctor] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Patient_NationalId] ON [identity].[Patient] ([NationalId]) WHERE [NationalId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PatientRepresentative_AppUserId] ON [identity].[PatientRepresentative] ([AppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PatientRepresentative_PatientId] ON [identity].[PatientRepresentative] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Room_RoomNumber] ON [identity].[Room] ([RoomNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomAccount_AccountCode] ON [identity].[RoomAccount] ([AccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomAccount_AppUserId] ON [identity].[RoomAccount] ([AppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomAccount_RoomId] ON [identity].[RoomAccount] ([RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RoomDeviceSession_RoomAccountId] ON [identity].[RoomDeviceSession] ([RoomAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomDeviceSession_SessionToken] ON [identity].[RoomDeviceSession] ([SessionToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_AppUserId] ON [identity].[Staff] ([AppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_StaffCode] ON [identity].[Staff] ([StaffCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_StaffShiftAssignment_RoomId] ON [identity].[StaffShiftAssignment] ([RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_StaffShiftAssignment_StaffId] ON [identity].[StaffShiftAssignment] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161536_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161536_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

