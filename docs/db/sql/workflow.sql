IF OBJECT_ID(N'[workflow].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'workflow') IS NULL EXEC(N'CREATE SCHEMA [workflow];');
    CREATE TABLE [workflow].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'workflow') IS NULL EXEC(N'CREATE SCHEMA [workflow];');
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [workflow].[BookingAttempt] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [SelectedDoctorId] uniqueidentifier NULL,
        [SelectedDoctorRankId] uniqueidentifier NULL,
        [CurrentStep] nvarchar(32) NOT NULL,
        [SelectedServiceDate] date NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BookingAttempt] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BookingAttempt_CurrentStep] CHECK ([CurrentStep] IN (N'ProfileSelection', N'DoctorSelection', N'SlotHeld', N'PaymentInitiated', N'Finalized', N'Abandoned')),
        CONSTRAINT [CK_BookingAttempt_Status] CHECK ([Status] IN (N'Draft', N'Held', N'WaitingPayment', N'Confirming', N'Confirmed', N'NeedsReselection', N'PaidUnallocated', N'Cancelled'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [workflow].[Operation] (
        [Id] uniqueidentifier NOT NULL,
        [InitiatedByUserId] uniqueidentifier NOT NULL,
        [BookingAttemptId] uniqueidentifier NULL,
        [OperationType] nvarchar(32) NOT NULL,
        [TargetEntityType] nvarchar(32) NOT NULL,
        [TargetEntityId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [FailureReason] nvarchar(2000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Operation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Operation_OperationType] CHECK ([OperationType] IN (N'HoldSlot', N'ConfirmBooking', N'RescheduleBooking', N'CancelBooking', N'AllocateStation', N'ApproveResult', N'ProcessRefund')),
        CONSTRAINT [CK_Operation_Status] CHECK ([Status] IN (N'Pending', N'Running', N'WaitingExternal', N'Succeeded', N'Failed', N'NeedsAction', N'ManualReview')),
        CONSTRAINT [CK_Operation_TargetEntityType] CHECK ([TargetEntityType] IN (N'BookingAttempt', N'Reservation', N'Appointment', N'BillingItem', N'QueueTicket', N'CLSResult')),
        CONSTRAINT [FK_Operation_BookingAttempt_BookingAttemptId] FOREIGN KEY ([BookingAttemptId]) REFERENCES [workflow].[BookingAttempt] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [workflow].[ApiIdempotencyRecord] (
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NULL,
        [CallerScope] nvarchar(128) NOT NULL,
        [OperationType] nvarchar(128) NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [RequestHash] nvarchar(64) NOT NULL,
        [ResponseStatusCode] int NULL,
        [ResponseBodyJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ApiIdempotencyRecord] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApiIdempotencyRecord_Operation_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [workflow].[Operation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [workflow].[AuditLog] (
        [Id] uniqueidentifier NOT NULL,
        [PerformedByUserId] uniqueidentifier NULL,
        [RoomAccountId] uniqueidentifier NULL,
        [RoomDeviceSessionId] uniqueidentifier NULL,
        [OperationId] uniqueidentifier NULL,
        [StaffShiftAssignmentId] uniqueidentifier NULL,
        [TableName] nvarchar(128) NOT NULL,
        [RecordId] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(32) NOT NULL,
        [DeviceId] nvarchar(128) NULL,
        [ClientIpAddress] nvarchar(45) NULL,
        [ChangedColumns] nvarchar(2000) NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [ChangeReason] nvarchar(500) NULL,
        [TimestampUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditLog_ActionType] CHECK ([ActionType] IN (N'INSERT', N'UPDATE', N'DELETE', N'CANCEL', N'REFUND', N'PUBLISH')),
        CONSTRAINT [FK_AuditLog_Operation_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [workflow].[Operation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [workflow].[OutboxMessage] (
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NULL,
        [EventType] nvarchar(32) NOT NULL,
        [AggregateType] nvarchar(32) NOT NULL,
        [AggregateId] uniqueidentifier NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [RetryCount] int NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OutboxMessage_AggregateType] CHECK ([AggregateType] IN (N'Reservation', N'Appointment', N'Payment', N'CLSResult')),
        CONSTRAINT [CK_OutboxMessage_EventType] CHECK ([EventType] IN (N'SlotHeldEvent', N'BookingConfirmedEvent', N'PaymentSettledEvent', N'CLSResultPublishedEvent')),
        CONSTRAINT [CK_OutboxMessage_Status] CHECK ([Status] IN (N'Pending', N'Processing', N'Published', N'DeadLetter')),
        CONSTRAINT [FK_OutboxMessage_Operation_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [workflow].[Operation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ApiIdempotencyRecord_OperationId] ON [workflow].[ApiIdempotencyRecord] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [UK_Idem] ON [workflow].[ApiIdempotencyRecord] ([CallerScope], [OperationType], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_AuditLog_OperationId] ON [workflow].[AuditLog] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_AuditLog_PerformedByUserId] ON [workflow].[AuditLog] ([PerformedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_AuditLog_RoomAccountId] ON [workflow].[AuditLog] ([RoomAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_AuditLog_RoomDeviceSessionId] ON [workflow].[AuditLog] ([RoomDeviceSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_AuditLog_StaffShiftAssignmentId] ON [workflow].[AuditLog] ([StaffShiftAssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BookingAttempt_AppUserId] ON [workflow].[BookingAttempt] ([AppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BookingAttempt_PatientId] ON [workflow].[BookingAttempt] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BookingAttempt_SelectedDoctorId] ON [workflow].[BookingAttempt] ([SelectedDoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BookingAttempt_SelectedDoctorRankId] ON [workflow].[BookingAttempt] ([SelectedDoctorRankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Operation_BookingAttemptId] ON [workflow].[Operation] ([BookingAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Operation_InitiatedByUserId] ON [workflow].[Operation] ([InitiatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_OutboxMessage_OperationId] ON [workflow].[OutboxMessage] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [workflow].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161958_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [workflow].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161958_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

