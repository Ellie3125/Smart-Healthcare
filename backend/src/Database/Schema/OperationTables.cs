using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Theo dõi tác vụ, idempotency, outbox và nhật ký kiểm toán (sơ đồ 7).</summary>
internal static class OperationTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("Operation", t =>
        {
            t.Id();
            t.Ref("InitiatedByUserId", "AppUser");
            t.OptRef("BookingAttemptId", "BookingAttempt");
            t.Enum("OperationType", 20,
                "HoldSlot", "ConfirmBooking", "RescheduleBooking", "CancelBooking", "AllocateStation", "ApproveResult", "ProcessRefund");
            t.Enum("TargetEntityType", 20, "BookingAttempt", "Reservation", "Appointment", "BillingItem", "QueueTicket", "CLSResult");
            t.Req<Guid>("TargetEntityId", "uniqueidentifier");
            t.Enum("Status", 20, "Pending", "Running", "WaitingExternal", "Succeeded", "Failed", "NeedsAction", "ManualReview")
                .Default("N'Pending'");
            t.Opt<string>("FailureReason", "nvarchar(1000)");
            t.UtcNow("CreatedAtUtc");
            t.OptUtc("CompletedAtUtc");
            t.RowVersion();
        });

        model.Table("ApiIdempotencyRecord", t =>
        {
            t.Id();
            t.OptRef("OperationId", "Operation");
            t.Req<string>("CallerScope", "nvarchar(100)");
            t.Req<string>("OperationType", "nvarchar(50)");
            t.Req<string>("IdempotencyKey", "varchar(100)");
            t.Req<string>("RequestHash", "char(64)"); // SHA256 của request body đã chuẩn hóa
            t.Opt<int>("ResponseStatusCode", "int");
            t.Opt<string>("ResponseBodyJson", "nvarchar(max)");
            t.UtcNow("CreatedAtUtc");
            t.Utc("ExpiresAtUtc");
            t.Rule("ResponseBodyJson", "ISJSON(ResponseBodyJson) = 1");
            t.UniqueKey("UK_Idem", "CallerScope", "OperationType", "IdempotencyKey");
        });

        model.Table("OutboxMessage", t =>
        {
            t.Id();
            t.OptRef("OperationId", "Operation");
            t.Enum("EventType", 30, "SlotHeldEvent", "BookingConfirmedEvent", "PaymentSettledEvent", "CLSResultPublishedEvent");
            t.Enum("AggregateType", 20, "Reservation", "Appointment", "Payment", "CLSResult");
            t.Req<Guid>("AggregateId", "uniqueidentifier");
            t.Req<string>("PayloadJson", "nvarchar(max)");
            t.Req<int>("RetryCount", "int").Default("0");
            t.Enum("Status", 15, "Pending", "Processing", "Published", "DeadLetter").Default("N'Pending'");
            t.UtcNow("CreatedAtUtc");
            t.OptUtc("ProcessedAtUtc");
            t.Rule("PayloadJson", "ISJSON(PayloadJson) = 1");
            t.Rule("RetryCount", "RetryCount >= 0");
            // Worker outbox quét các tin chưa gửi theo thứ tự tạo.
            t.Index("IX_OutboxMessage_Pending", "Status IN (N'Pending', N'Processing')", "CreatedAtUtc");
        });

        model.Table("AuditLog", t =>
        {
            t.Id();
            t.OptRef("PerformedByUserId", "AppUser");
            t.OptRef("RoomAccountId", "RoomAccount");
            t.OptRef("RoomDeviceSessionId", "RoomDeviceSession");
            t.OptRef("OperationId", "Operation");
            t.OptRef("StaffShiftAssignmentId", "StaffShiftAssignment");
            t.Req<string>("TableName", "varchar(128)");
            t.Req<Guid>("RecordId", "uniqueidentifier");
            t.Enum("ActionType", 10, "INSERT", "UPDATE", "DELETE", "CANCEL", "REFUND", "PUBLISH");
            t.Opt<string>("DeviceId", "nvarchar(100)");
            t.Opt<string>("ClientIpAddress", "varchar(45)");
            t.Opt<string>("ChangedColumns", "nvarchar(max)");
            t.Opt<string>("OldValuesJson", "nvarchar(max)");
            t.Opt<string>("NewValuesJson", "nvarchar(max)");
            t.Opt<string>("ChangeReason", "nvarchar(500)");
            t.UtcNow("TimestampUtc");
            t.Rule("OldValuesJson", "ISJSON(OldValuesJson) = 1");
            t.Rule("NewValuesJson", "ISJSON(NewValuesJson) = 1");
            t.Index("IX_AuditLog_Record", null, "TableName", "RecordId", "TimestampUtc");
        });
    }
}
