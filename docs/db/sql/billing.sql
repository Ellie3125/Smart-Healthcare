IF OBJECT_ID(N'[billing].[__EFMigrationsHistory]') IS NULL
BEGIN
    IF SCHEMA_ID(N'billing') IS NULL EXEC(N'CREATE SCHEMA [billing];');
    CREATE TABLE [billing].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    IF SCHEMA_ID(N'billing') IS NULL EXEC(N'CREATE SCHEMA [billing];');
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[BillingItem] (
        [Id] uniqueidentifier NOT NULL,
        [EncounterId] uniqueidentifier NULL,
        [AppointmentId] uniqueidentifier NULL,
        [ServiceOrderItemId] uniqueidentifier NULL,
        [PriceListItemId] uniqueidentifier NOT NULL,
        [ItemType] nvarchar(32) NOT NULL,
        [ItemNameSnapshot] nvarchar(200) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BillingItem] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BillingItem_ItemType] CHECK ([ItemType] IN (N'ConsultationFee', N'CLSService')),
        CONSTRAINT [CK_BillingItem_Status] CHECK ([Status] IN (N'Pending', N'PartiallyPaid', N'Paid', N'Cancelled', N'Waived')),
        CONSTRAINT [CK_BillingItem_TotalAmount] CHECK ([TotalAmount] = [UnitPrice] * [Quantity])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[PaymentIntent] (
        [Id] uniqueidentifier NOT NULL,
        [IntentNumber] nvarchar(50) NOT NULL,
        [PayerAppUserId] uniqueidentifier NOT NULL,
        [PatientId] uniqueidentifier NOT NULL,
        [BookingAttemptId] uniqueidentifier NULL,
        [EncounterId] uniqueidentifier NULL,
        [TotalExpectedAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(32) NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [PaymentType] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentIntent] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentIntent_Currency] CHECK ([Currency] IN (N'VND')),
        CONSTRAINT [CK_PaymentIntent_PaymentType] CHECK ([PaymentType] IN (N'InitialConsultation', N'CLSOrderBatch')),
        CONSTRAINT [CK_PaymentIntent_Provider] CHECK ([Provider] IN (N'VNPT', N'MoMo', N'Counter')),
        CONSTRAINT [CK_PaymentIntent_Status] CHECK ([Status] IN (N'Pending', N'Processing', N'Succeeded', N'Failed', N'Unknown', N'Expired', N'Cancelled'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[PaymentTransaction] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentIntentId] uniqueidentifier NOT NULL,
        [TransactionReference] nvarchar(128) NOT NULL,
        [ProviderTransactionId] nvarchar(128) NULL,
        [TransactionAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(32) NOT NULL,
        [PaymentStatus] nvarchar(32) NOT NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [ProviderRawData] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_PaymentTransaction] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentTransaction_Currency] CHECK ([Currency] IN (N'VND')),
        CONSTRAINT [CK_PaymentTransaction_PaymentStatus] CHECK ([PaymentStatus] IN (N'Pending', N'Success', N'Failed', N'Unknown')),
        CONSTRAINT [FK_PaymentTransaction_PaymentIntent_PaymentIntentId] FOREIGN KEY ([PaymentIntentId]) REFERENCES [billing].[PaymentIntent] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[PaymentAllocation] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentTransactionId] uniqueidentifier NOT NULL,
        [BillingItemId] uniqueidentifier NOT NULL,
        [AllocatedAmount] decimal(18,2) NOT NULL,
        [AllocatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentAllocation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentAllocation_AllocatedAmount] CHECK ([AllocatedAmount] > 0),
        CONSTRAINT [FK_PaymentAllocation_BillingItem_BillingItemId] FOREIGN KEY ([BillingItemId]) REFERENCES [billing].[BillingItem] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocation_PaymentTransaction_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [billing].[PaymentTransaction] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[PaymentEvent] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentTransactionId] uniqueidentifier NULL,
        [Provider] nvarchar(32) NOT NULL,
        [EventId] nvarchar(128) NOT NULL,
        [EventType] nvarchar(32) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [ReceivedAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PaymentEvent] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentEvent_EventType] CHECK ([EventType] IN (N'PaymentSuccess', N'PaymentRefund')),
        CONSTRAINT [CK_PaymentEvent_Provider] CHECK ([Provider] IN (N'VNPT', N'MoMo')),
        CONSTRAINT [CK_PaymentEvent_Status] CHECK ([Status] IN (N'Received', N'Processed', N'DuplicateIgnored', N'Error')),
        CONSTRAINT [FK_PaymentEvent_PaymentTransaction_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [billing].[PaymentTransaction] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[RefundRequest] (
        [Id] uniqueidentifier NOT NULL,
        [BillingItemId] uniqueidentifier NOT NULL,
        [OriginalPaymentTransactionId] uniqueidentifier NOT NULL,
        [AppointmentId] uniqueidentifier NULL,
        [RequestedAmount] decimal(18,2) NOT NULL,
        [RefundReason] nvarchar(32) NOT NULL,
        [PolicyRuleApplied] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [RequestedAtUtc] datetime2 NOT NULL,
        [DecisionByStaffId] uniqueidentifier NULL,
        [DecidedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RefundRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RefundRequest_PolicyRule] CHECK (([RefundReason] = N'CancelledBefore0500' AND [PolicyRuleApplied] = N'FullRefundPre0500') OR ([RefundReason] <> N'CancelledBefore0500' AND [PolicyRuleApplied] = N'CustomApproval')),
        CONSTRAINT [CK_RefundRequest_PolicyRuleApplied] CHECK ([PolicyRuleApplied] IN (N'FullRefundPre0500', N'CustomApproval')),
        CONSTRAINT [CK_RefundRequest_RefundReason] CHECK ([RefundReason] IN (N'CancelledBefore0500', N'PaidUnallocated', N'DuplicatePayment', N'HospitalCancelled', N'SystemError')),
        CONSTRAINT [CK_RefundRequest_Status] CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected', N'Settled', N'Failed')),
        CONSTRAINT [FK_RefundRequest_BillingItem_BillingItemId] FOREIGN KEY ([BillingItemId]) REFERENCES [billing].[BillingItem] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RefundRequest_PaymentTransaction_OriginalPaymentTransactionId] FOREIGN KEY ([OriginalPaymentTransactionId]) REFERENCES [billing].[PaymentTransaction] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE TABLE [billing].[RefundTransaction] (
        [Id] uniqueidentifier NOT NULL,
        [RefundRequestId] uniqueidentifier NOT NULL,
        [OriginalPaymentTransactionId] uniqueidentifier NOT NULL,
        [ProviderRefundTransactionId] nvarchar(128) NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ProcessedAtUtc] datetime2 NOT NULL,
        [ProviderResponsePayload] nvarchar(max) NULL,
        CONSTRAINT [PK_RefundTransaction] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RefundTransaction_Currency] CHECK ([Currency] IN (N'VND')),
        CONSTRAINT [CK_RefundTransaction_Status] CHECK ([Status] IN (N'Success', N'Failed')),
        CONSTRAINT [FK_RefundTransaction_PaymentTransaction_OriginalPaymentTransactionId] FOREIGN KEY ([OriginalPaymentTransactionId]) REFERENCES [billing].[PaymentTransaction] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RefundTransaction_RefundRequest_RefundRequestId] FOREIGN KEY ([RefundRequestId]) REFERENCES [billing].[RefundRequest] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BillingItem_AppointmentId] ON [billing].[BillingItem] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BillingItem_EncounterId] ON [billing].[BillingItem] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BillingItem_PriceListItemId] ON [billing].[BillingItem] ([PriceListItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_BillingItem_ServiceOrderItemId] ON [billing].[BillingItem] ([ServiceOrderItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocation_BillingItemId] ON [billing].[PaymentAllocation] ([BillingItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocation_PaymentTransactionId] ON [billing].[PaymentAllocation] ([PaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentEvent_PayloadHash] ON [billing].[PaymentEvent] ([PayloadHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentEvent_PaymentTransactionId] ON [billing].[PaymentEvent] ([PaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [UK_ProviderEvent] ON [billing].[PaymentEvent] ([Provider], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentIntent_BookingAttemptId] ON [billing].[PaymentIntent] ([BookingAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentIntent_EncounterId] ON [billing].[PaymentIntent] ([EncounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentIntent_IntentNumber] ON [billing].[PaymentIntent] ([IntentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentIntent_PatientId] ON [billing].[PaymentIntent] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentIntent_PayerAppUserId] ON [billing].[PaymentIntent] ([PayerAppUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_PaymentTransaction_PaymentIntentId] ON [billing].[PaymentTransaction] ([PaymentIntentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentTransaction_ProviderTransactionId] ON [billing].[PaymentTransaction] ([ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransaction_TransactionReference] ON [billing].[PaymentTransaction] ([TransactionReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RefundRequest_AppointmentId] ON [billing].[RefundRequest] ([AppointmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RefundRequest_BillingItemId] ON [billing].[RefundRequest] ([BillingItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RefundRequest_DecisionByStaffId] ON [billing].[RefundRequest] ([DecisionByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RefundRequest_OriginalPaymentTransactionId] ON [billing].[RefundRequest] ([OriginalPaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE INDEX [IX_RefundTransaction_OriginalPaymentTransactionId] ON [billing].[RefundTransaction] ([OriginalPaymentTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_RefundTransaction_ProviderRefundTransactionId] ON [billing].[RefundTransaction] ([ProviderRefundTransactionId]) WHERE [ProviderRefundTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefundTransaction_RefundRequestId] ON [billing].[RefundTransaction] ([RefundRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [billing].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029161955_InitialCreate_ErdV22'
)
BEGIN
    INSERT INTO [billing].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029161955_InitialCreate_ErdV22', N'10.0.14');
END;

COMMIT;
GO

