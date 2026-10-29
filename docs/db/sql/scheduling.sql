IF OBJECT_ID(N'[scheduling].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'scheduling') IS NULL EXEC(N'CREATE SCHEMA [scheduling];');
    CREATE TABLE [scheduling].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'scheduling') IS NULL EXEC(N'CREATE SCHEMA [scheduling];');
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[DoctorSessionSlotQuota] (
        [Id] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [AssignedRoomId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [Capacity] int NOT NULL,
        [HeldCount] int NOT NULL,
        [AdmittedCount] int NOT NULL,
        [LockedCancelledCount] int NOT NULL,
        [SessionStartTimeUtc] datetime2 NOT NULL,
        [BookingCutoffTimeUtc] datetime2 NOT NULL,
        [RescheduleCutoffTimeUtc] datetime2 NOT NULL,
        [CheckInDeadlineUtc] datetime2 NULL,
        [SessionStatus] nvarchar(32) NOT NULL,
        [IsOpen] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_DoctorSessionSlotQuota] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DoctorSessionSlotQuota_AdmittedCount] CHECK ([AdmittedCount] >= 0),
        CONSTRAINT [CK_DoctorSessionSlotQuota_Capacity] CHECK ([HeldCount] + [AdmittedCount] + [LockedCancelledCount] <= [Capacity]),
        CONSTRAINT [CK_DoctorSessionSlotQuota_HeldCount] CHECK ([HeldCount] >= 0),
        CONSTRAINT [CK_DoctorSessionSlotQuota_LockedCancelledCount] CHECK ([LockedCancelledCount] >= 0),
        CONSTRAINT [CK_DoctorSessionSlotQuota_SessionStatus] CHECK ([SessionStatus] IN (N'Scheduled', N'InProgress', N'Completed', N'Cancelled'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[ServiceStation] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [StationCode] nvarchar(50) NOT NULL,
        [StationName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ServiceStation] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[Reservation] (
        [Id] uniqueidentifier NOT NULL,
        [SessionQuotaId] uniqueidentifier NOT NULL,
        [BookingAttemptId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [AppUserId] uniqueidentifier NOT NULL,
        [BookingChannel] nvarchar(32) NOT NULL,
        [ReservedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [IdempotencyKey] nvarchar(128) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Reservation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reservation_BookingChannel] CHECK ([BookingChannel] IN (N'Web', N'App', N'Chatbot', N'Counter')),
        CONSTRAINT [CK_Reservation_Status] CHECK ([Status] IN (N'Held', N'Confirmed', N'Expired', N'Cancelled')),
        CONSTRAINT [FK_Reservation_DoctorSessionSlotQuota_SessionQuotaId] FOREIGN KEY ([SessionQuotaId]) REFERENCES [scheduling].[DoctorSessionSlotQuota] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[QueueTicket] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [TargetRoomId] uniqueidentifier NOT NULL,
        [TargetStationId] uniqueidentifier NULL,
        [ServiceOrderId] uniqueidentifier NULL,
        [ServiceOrderItemId] uniqueidentifier NULL,
        [TicketNumber] nvarchar(50) NOT NULL,
        [TicketType] nvarchar(32) NOT NULL,
        [SequenceNumber] int NOT NULL,
        [QueueStatus] nvarchar(32) NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [CalledAtUtc] datetime2 NULL,
        [ServedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_QueueTicket] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_QueueTicket_QueueStatus] CHECK ([QueueStatus] IN (N'Waiting', N'Called', N'Deferred', N'InProgress', N'Finished', N'Cancelled')),
        CONSTRAINT [CK_QueueTicket_TicketType] CHECK ([TicketType] IN (N'InitialConsultation', N'CLS', N'ReturnConsultation')),
        CONSTRAINT [FK_QueueTicket_ServiceStation_TargetStationId] FOREIGN KEY ([TargetStationId]) REFERENCES [scheduling].[ServiceStation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[Appointment] (
        [Id] uniqueidentifier NOT NULL,
        [ReservationId] uniqueidentifier NOT NULL,
        [SessionQuotaId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [RescheduledFromAppointmentId] uniqueidentifier NULL,
        [AppointmentNumber] nvarchar(50) NOT NULL,
        [AppliedConsultationFee] decimal(18,2) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ConfirmedAtUtc] datetime2 NOT NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CancellationReason] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Appointment] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Appointment_Status] CHECK ([Status] IN (N'Scheduled', N'CheckedIn', N'Completed', N'Cancelled', N'Absent', N'Rescheduled')),
        CONSTRAINT [FK_Appointment_Appointment_RescheduledFromAppointmentId] FOREIGN KEY ([RescheduledFromAppointmentId]) REFERENCES [scheduling].[Appointment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Appointment_DoctorSessionSlotQuota_SessionQuotaId] FOREIGN KEY ([SessionQuotaId]) REFERENCES [scheduling].[DoctorSessionSlotQuota] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Appointment_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [scheduling].[Reservation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[ActivePatientEngagement] (
        [PatientId] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NOT NULL,
        [CurrentStationId] uniqueidentifier NOT NULL,
        [CurrentTicketId] uniqueidentifier NOT NULL,
        [AcquiredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ActivePatientEngagement] PRIMARY KEY ([PatientId]),
        CONSTRAINT [FK_ActivePatientEngagement_QueueTicket_CurrentTicketId] FOREIGN KEY ([CurrentTicketId]) REFERENCES [scheduling].[QueueTicket] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActivePatientEngagement_ServiceStation_CurrentStationId] FOREIGN KEY ([CurrentStationId]) REFERENCES [scheduling].[ServiceStation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[ActiveStationAllocation] (
        [StationId] uniqueidentifier NOT NULL,
        [CurrentPatientId] uniqueidentifier NOT NULL,
        [CurrentTicketId] uniqueidentifier NOT NULL,
        [AllocatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ActiveStationAllocation] PRIMARY KEY ([StationId]),
        CONSTRAINT [FK_ActiveStationAllocation_QueueTicket_CurrentTicketId] FOREIGN KEY ([CurrentTicketId]) REFERENCES [scheduling].[QueueTicket] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActiveStationAllocation_ServiceStation_StationId] FOREIGN KEY ([StationId]) REFERENCES [scheduling].[ServiceStation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [scheduling].[ActiveBookingClaim] (
        [Id] uniqueidentifier NOT NULL,
        [SessionQuotaId] uniqueidentifier NOT NULL,
        [DoctorId] uniqueidentifier NOT NULL,
        [ServiceDate] date NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [ReservationId] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NULL,
        [ClaimSource] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ReleasedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ActiveBookingClaim] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ActiveBookingClaim_ClaimSource] CHECK ([ClaimSource] IN (N'App', N'Web', N'Chatbot', N'Counter')),
        CONSTRAINT [FK_ActiveBookingClaim_Appointment_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [scheduling].[Appointment] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActiveBookingClaim_DoctorSessionSlotQuota_SessionQuotaId] FOREIGN KEY ([SessionQuotaId]) REFERENCES [scheduling].[DoctorSessionSlotQuota] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ActiveBookingClaim_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [scheduling].[Reservation] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActiveBookingClaim_AppointmentId] ON [scheduling].[ActiveBookingClaim] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActiveBookingClaim_DoctorId] ON [scheduling].[ActiveBookingClaim] ([DoctorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ActiveBookingClaim_DoctorId_ServiceDate_PatientId] ON [scheduling].[ActiveBookingClaim] ([DoctorId], [ServiceDate], [PatientId]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActiveBookingClaim_PatientId] ON [scheduling].[ActiveBookingClaim] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActiveBookingClaim_ReservationId] ON [scheduling].[ActiveBookingClaim] ([ReservationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActiveBookingClaim_SessionQuotaId] ON [scheduling].[ActiveBookingClaim] ([SessionQuotaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ActivePatientEngagement_CurrentStationId] ON [scheduling].[ActivePatientEngagement] ([CurrentStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ActivePatientEngagement_CurrentTicketId] ON [scheduling].[ActivePatientEngagement] ([CurrentTicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ActivePatientEngagement_EncounterId] ON [scheduling].[ActivePatientEngagement] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ActiveStationAllocation_CurrentPatientId] ON [scheduling].[ActiveStationAllocation] ([CurrentPatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ActiveStationAllocation_CurrentTicketId] ON [scheduling].[ActiveStationAllocation] ([CurrentTicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Appointment_AppointmentNumber] ON [scheduling].[Appointment] ([AppointmentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Appointment_PatientId] ON [scheduling].[Appointment] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Appointment_RescheduledFromAppointmentId] ON [scheduling].[Appointment] ([RescheduledFromAppointmentId]) WHERE [RescheduledFromAppointmentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Appointment_ReservationId] ON [scheduling].[Appointment] ([ReservationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Appointment_SessionQuotaId] ON [scheduling].[Appointment] ([SessionQuotaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_DoctorSessionSlotQuota_AssignedRoomId] ON [scheduling].[DoctorSessionSlotQuota] ([AssignedRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DoctorSessionSlotQuota_DoctorId_ServiceDate] ON [scheduling].[DoctorSessionSlotQuota] ([DoctorId], [ServiceDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_QueueTicket_EncounterId] ON [scheduling].[QueueTicket] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_QueueTicket_ServiceOrderId] ON [scheduling].[QueueTicket] ([ServiceOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_QueueTicket_ServiceOrderItemId] ON [scheduling].[QueueTicket] ([ServiceOrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_QueueTicket_TargetRoomId] ON [scheduling].[QueueTicket] ([TargetRoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_QueueTicket_TargetStationId] ON [scheduling].[QueueTicket] ([TargetStationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Reservation_AppUserId] ON [scheduling].[Reservation] ([AppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Reservation_BookingAttemptId] ON [scheduling].[Reservation] ([BookingAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reservation_IdempotencyKey] ON [scheduling].[Reservation] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Reservation_PatientId] ON [scheduling].[Reservation] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_Reservation_SessionQuotaId] ON [scheduling].[Reservation] ([SessionQuotaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_ServiceStation_RoomId] ON [scheduling].[ServiceStation] ([RoomId]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ServiceStation_StationCode] ON [scheduling].[ServiceStation] ([StationCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [scheduling].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161948_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [scheduling].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161948_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

