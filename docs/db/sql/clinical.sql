IF OBJECT_ID(N'[clinical].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'clinical') IS NULL EXEC(N'CREATE SCHEMA [clinical];');
    CREATE TABLE [clinical].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'clinical') IS NULL EXEC(N'CREATE SCHEMA [clinical];');
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[Encounter] (
        [Id] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [OriginalDoctorId] uniqueidentifier NOT NULL,
        [OriginalRoomId] uniqueidentifier NOT NULL,
        [SpecialtySnapshot] nvarchar(200) NOT NULL,
        [DoctorRankSnapshot] nvarchar(200) NOT NULL,
        [CheckedInAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Encounter] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Encounter_Status] CHECK ([Status] IN (N'InProgress', N'WaitingCLS', N'ReturningDoctor', N'Completed', N'Abandoned'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[EncounterClinicalRecord] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ChiefComplaint] nvarchar(max) NULL,
        [Symptoms] nvarchar(max) NULL,
        [MedicalHistory] nvarchar(max) NULL,
        [ClinicalExaminationNotes] nvarchar(max) NULL,
        [InitialClinicalImpression] nvarchar(max) NULL,
        [PostCLSConclusion] nvarchar(max) NULL,
        [TreatmentPlan] nvarchar(max) NULL,
        [FollowUpInstructions] nvarchar(max) NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [LastUpdatedUtc] datetime2 NOT NULL,
        [UpdatedByDoctorId] uniqueidentifier NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_EncounterClinicalRecord] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EncounterClinicalRecord_Status] CHECK ([Status] IN (N'Draft', N'Published')),
        CONSTRAINT [FK_EncounterClinicalRecord_Encounter_EncounterId] FOREIGN KEY ([EncounterId]) REFERENCES [clinical].[Encounter] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[EncounterDiagnosis] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [DiagnosisType] nvarchar(32) NOT NULL,
        [ICD10Code] nvarchar(10) NULL,
        [DiagnosisName] nvarchar(500) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [ClinicalNotes] nvarchar(max) NULL,
        [DiagnosedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EncounterDiagnosis] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_EncounterDiagnosis_DiagnosisType] CHECK ([DiagnosisType] IN (N'Initial', N'Final')),
        CONSTRAINT [FK_EncounterDiagnosis_Encounter_EncounterId] FOREIGN KEY ([EncounterId]) REFERENCES [clinical].[Encounter] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[Prescription] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [PrescribedByDoctorId] uniqueidentifier NOT NULL,
        [PrescriptionCode] nvarchar(50) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [GeneralInstructions] nvarchar(2000) NULL,
        [IssuedAtUtc] datetime2 NULL,
        [PublishedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Prescription] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Prescription_Status] CHECK ([Status] IN (N'Draft', N'Issued', N'Cancelled')),
        CONSTRAINT [FK_Prescription_Encounter_EncounterId] FOREIGN KEY ([EncounterId]) REFERENCES [clinical].[Encounter] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[ServiceOrder] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [OrderRoundNumber] int NOT NULL,
        [OrderedAtUtc] datetime2 NOT NULL,
        [PrescribedByDoctorId] uniqueidentifier NOT NULL,
        [OrderBatchStatus] nvarchar(32) NOT NULL,
        [HasActiveReturnTicket] bit NOT NULL,
        CONSTRAINT [PK_ServiceOrder] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ServiceOrder_OrderBatchStatus] CHECK ([OrderBatchStatus] IN (N'Draft', N'Confirmed', N'Completed', N'Cancelled')),
        CONSTRAINT [FK_ServiceOrder_Encounter_EncounterId] FOREIGN KEY ([EncounterId]) REFERENCES [clinical].[Encounter] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[VitalSign] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [WeightKg] decimal(5,2) NOT NULL,
        [HeightCm] decimal(5,2) NOT NULL,
        [SystolicBP] decimal(5,2) NOT NULL,
        [DiastolicBP] decimal(5,2) NOT NULL,
        [PulseRate] int NOT NULL,
        [BodyTemperature] decimal(5,2) NOT NULL,
        [SpO2] decimal(5,2) NOT NULL,
        [RecordedByStaffId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VitalSign] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VitalSign_Encounter_EncounterId] FOREIGN KEY ([EncounterId]) REFERENCES [clinical].[Encounter] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[PrescriptionItem] (
        [Id] uniqueidentifier NOT NULL,
        [PrescriptionId] uniqueidentifier NOT NULL,
        [MedicationId] uniqueidentifier NOT NULL,
        [MedicationNameSnapshot] nvarchar(200) NOT NULL,
        [ActiveIngredientSnapshot] nvarchar(200) NOT NULL,
        [Dosage] nvarchar(200) NOT NULL,
        [Route] nvarchar(32) NOT NULL,
        [Frequency] nvarchar(200) NOT NULL,
        [DurationDays] int NOT NULL,
        [Quantity] int NOT NULL,
        [UsageInstructions] nvarchar(2000) NULL,
        CONSTRAINT [PK_PrescriptionItem] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PrescriptionItem_Route] CHECK ([Route] IN (N'Oral', N'Topical', N'Injection', N'Inhalation')),
        CONSTRAINT [FK_PrescriptionItem_Prescription_PrescriptionId] FOREIGN KEY ([PrescriptionId]) REFERENCES [clinical].[Prescription] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[ServiceOrderItem] (
        [Id] uniqueidentifier NOT NULL,
        [ServiceOrderId] uniqueidentifier NOT NULL,
        [MedicalServiceId] uniqueidentifier NOT NULL,
        [ServiceNameSnapshot] nvarchar(200) NOT NULL,
        [AppliedPrice] decimal(18,2) NOT NULL,
        [ItemStatus] nvarchar(32) NOT NULL,
        [IsMandatory] bit NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ServiceOrderItem] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ServiceOrderItem_ItemStatus] CHECK ([ItemStatus] IN (N'Ordered', N'SampleCollected', N'InProgress', N'ServiceFinished', N'Completed', N'Cancelled')),
        CONSTRAINT [FK_ServiceOrderItem_ServiceOrder_ServiceOrderId] FOREIGN KEY ([ServiceOrderId]) REFERENCES [clinical].[ServiceOrder] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[CLSResult] (
        [Id] uniqueidentifier NOT NULL,
        [ServiceOrderItemId] uniqueidentifier NOT NULL,
        [ResultSummary] nvarchar(2000) NULL,
        [DetailedFindings] nvarchar(max) NULL,
        [ResultStatus] nvarchar(32) NOT NULL,
        [PerformedByStaffId] uniqueidentifier NOT NULL,
        [ApprovedByStaffId] uniqueidentifier NULL,
        [PerformedAtUtc] datetime2 NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [PublishedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CLSResult] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CLSResult_ResultStatus] CHECK ([ResultStatus] IN (N'Draft', N'Approved', N'Published', N'Cancelled')),
        CONSTRAINT [FK_CLSResult_ServiceOrderItem_ServiceOrderItemId] FOREIGN KEY ([ServiceOrderItemId]) REFERENCES [clinical].[ServiceOrderItem] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [clinical].[ResultAttachment] (
        [Id] uniqueidentifier NOT NULL,
        [CLSResultId] uniqueidentifier NOT NULL,
        [R2StorageKey] nvarchar(1024) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [FileContentType] nvarchar(127) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [AttachmentVersion] int NOT NULL,
        [IsDraft] bit NOT NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ResultAttachment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResultAttachment_CLSResult_CLSResultId] FOREIGN KEY ([CLSResultId]) REFERENCES [clinical].[CLSResult] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_CLSResult_ApprovedByStaffId] ON [clinical].[CLSResult] ([ApprovedByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_CLSResult_PerformedByStaffId] ON [clinical].[CLSResult] ([PerformedByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CLSResult_ServiceOrderItemId] ON [clinical].[CLSResult] ([ServiceOrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Encounter_AppointmentId] ON [clinical].[Encounter] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Encounter_OriginalDoctorId] ON [clinical].[Encounter] ([OriginalDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Encounter_OriginalRoomId] ON [clinical].[Encounter] ([OriginalRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Encounter_PatientId] ON [clinical].[Encounter] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EncounterClinicalRecord_EncounterId] ON [clinical].[EncounterClinicalRecord] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_EncounterClinicalRecord_UpdatedByDoctorId] ON [clinical].[EncounterClinicalRecord] ([UpdatedByDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_EncounterDiagnosis_EncounterId] ON [clinical].[EncounterDiagnosis] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Prescription_EncounterId] ON [clinical].[Prescription] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Prescription_PrescribedByDoctorId] ON [clinical].[Prescription] ([PrescribedByDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Prescription_PrescriptionCode] ON [clinical].[Prescription] ([PrescriptionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PrescriptionItem_MedicationId] ON [clinical].[PrescriptionItem] ([MedicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PrescriptionItem_PrescriptionId] ON [clinical].[PrescriptionItem] ([PrescriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ResultAttachment_CLSResultId] ON [clinical].[ResultAttachment] ([CLSResultId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ResultAttachment_R2StorageKey] ON [clinical].[ResultAttachment] ([R2StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ServiceOrder_EncounterId] ON [clinical].[ServiceOrder] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ServiceOrder_PrescribedByDoctorId] ON [clinical].[ServiceOrder] ([PrescribedByDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ServiceOrderItem_MedicalServiceId] ON [clinical].[ServiceOrderItem] ([MedicalServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ServiceOrderItem_ServiceOrderId] ON [clinical].[ServiceOrderItem] ([ServiceOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_VitalSign_EncounterId] ON [clinical].[VitalSign] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_VitalSign_RecordedByStaffId] ON [clinical].[VitalSign] ([RecordedByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [clinical].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161951_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [clinical].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161951_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

