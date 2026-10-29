IF OBJECT_ID(N'[catalog].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
    CREATE TABLE [catalog].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog];');
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[DoctorRank] (
        [Id] uniqueidentifier NOT NULL,
        [RankCode] nvarchar(32) NOT NULL,
        [RankName] nvarchar(200) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DoctorRank] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DoctorRank_RankCode] CHECK ([RankCode] IN (N'SpecialistI', N'SpecialistII', N'Master', N'Doctor', N'AssociateProf', N'Professor'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[MedicalService] (
        [Id] uniqueidentifier NOT NULL,
        [ServiceCode] nvarchar(50) NOT NULL,
        [ServiceName] nvarchar(200) NOT NULL,
        [ServiceCategory] nvarchar(32) NOT NULL,
        [DefaultRoomId] uniqueidentifier NOT NULL,
        [IsMandatoryDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MedicalService] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MedicalService_ServiceCategory] CHECK ([ServiceCategory] IN (N'Laboratory', N'Radiology', N'Ultrasound', N'Endoscopy'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[MedicationCatalog] (
        [Id] uniqueidentifier NOT NULL,
        [MedicationCode] nvarchar(50) NOT NULL,
        [MedicationName] nvarchar(200) NOT NULL,
        [ActiveIngredient] nvarchar(200) NOT NULL,
        [DosageForm] nvarchar(32) NOT NULL,
        [Strength] nvarchar(200) NOT NULL,
        [Unit] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MedicationCatalog] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MedicationCatalog_DosageForm] CHECK ([DosageForm] IN (N'Tablet', N'Capsule', N'Liquid', N'Injection')),
        CONSTRAINT [CK_MedicationCatalog_Unit] CHECK ([Unit] IN (N'Vien', N'Goi', N'Chai', N'Ong'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[PriceList] (
        [Id] uniqueidentifier NOT NULL,
        [PriceListCode] nvarchar(50) NOT NULL,
        [PriceListName] nvarchar(200) NOT NULL,
        [Currency] nvarchar(32) NOT NULL,
        [EffectiveFromUtc] datetime2 NOT NULL,
        [EffectiveToUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PriceList] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PriceList_Currency] CHECK ([Currency] IN (N'VND'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[Specialty] (
        [Id] uniqueidentifier NOT NULL,
        [SpecialtyCode] nvarchar(50) NOT NULL,
        [SpecialtyName] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Specialty] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[MedicalServiceRoom] (
        [Id] uniqueidentifier NOT NULL,
        [MedicalServiceId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [IsDefaultRoom] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MedicalServiceRoom] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalServiceRoom_MedicalService_MedicalServiceId] FOREIGN KEY ([MedicalServiceId]) REFERENCES [catalog].[MedicalService] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [catalog].[PriceListItem] (
        [Id] uniqueidentifier NOT NULL,
        [PriceListId] uniqueidentifier NOT NULL,
        [ItemType] nvarchar(32) NOT NULL,
        [DoctorRankId] uniqueidentifier NULL,
        [DoctorId] uniqueidentifier NULL,
        [MedicalServiceId] uniqueidentifier NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PriceListItem] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PriceListItem_ItemType] CHECK ([ItemType] IN (N'ConsultationFee', N'CLSService')),
        CONSTRAINT [CK_PriceListItem_ItemTypeTarget] CHECK (([ItemType] = N'ConsultationFee' AND ([DoctorRankId] IS NOT NULL OR [DoctorId] IS NOT NULL) AND [MedicalServiceId] IS NULL) OR ([ItemType] = N'CLSService' AND [MedicalServiceId] IS NOT NULL AND [DoctorRankId] IS NULL AND [DoctorId] IS NULL)),
        CONSTRAINT [CK_PriceListItem_UnitPrice] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [FK_PriceListItem_DoctorRank_DoctorRankId] FOREIGN KEY ([DoctorRankId]) REFERENCES [catalog].[DoctorRank] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PriceListItem_MedicalService_MedicalServiceId] FOREIGN KEY ([MedicalServiceId]) REFERENCES [catalog].[MedicalService] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PriceListItem_PriceList_PriceListId] FOREIGN KEY ([PriceListId]) REFERENCES [catalog].[PriceList] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DoctorRank_RankCode] ON [catalog].[DoctorRank] ([RankCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_MedicalService_DefaultRoomId] ON [catalog].[MedicalService] ([DefaultRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicalService_ServiceCode] ON [catalog].[MedicalService] ([ServiceCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_MedicalServiceRoom_MedicalServiceId] ON [catalog].[MedicalServiceRoom] ([MedicalServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_MedicalServiceRoom_RoomId] ON [catalog].[MedicalServiceRoom] ([RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicationCatalog_MedicationCode] ON [catalog].[MedicationCatalog] ([MedicationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceList_PriceListCode] ON [catalog].[PriceList] ([PriceListCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PriceListItem_DoctorId] ON [catalog].[PriceListItem] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PriceListItem_DoctorRankId] ON [catalog].[PriceListItem] ([DoctorRankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PriceListItem_MedicalServiceId] ON [catalog].[PriceListItem] ([MedicalServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PriceListItem_PriceListId] ON [catalog].[PriceListItem] ([PriceListId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Specialty_SpecialtyCode] ON [catalog].[Specialty] ([SpecialtyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161945_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161945_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

