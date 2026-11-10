namespace Notifications.Contracts;

/// <summary>
/// Ghi và tra workflow.Operation (Giải pháp v2.14 §2.2, §2.3): một lệnh đã nhận đang xử lý đến đâu.
/// <see cref="StartAsync"/> và <see cref="CompleteAsync"/> phải chạy bên trong <c>IUnitOfWork</c>, cùng transaction với tác dụng nghiệp vụ.
/// </summary>
public interface IOperationTracker
{
    Task StartAsync(StartOperation operation, CancellationToken ct);

    Task CompleteAsync(Guid operationId, OperationStatus status, string? failureReason, CancellationToken ct);

    Task<OperationView?> GetAsync(Guid operationId, CancellationToken ct);
}

public sealed record StartOperation(Guid OperationId, Guid InitiatedByUserId, OperationType OperationType,
    TargetEntityType TargetEntityType, Guid TargetEntityId, Guid? BookingAttemptId);

public sealed record OperationView(Guid OperationId, Guid InitiatedByUserId, Guid? BookingAttemptId, OperationType OperationType,
    TargetEntityType TargetEntityType, Guid TargetEntityId, OperationStatus Status, string? FailureReason,
    DateTime CreatedAtUtc, DateTime? CompletedAtUtc);

// Cùng tên giá trị với enum trong Notifications.Domain (ERD v2.4); Infrastructure ánh xạ theo tên.
public enum OperationType { HoldSlot, ConfirmBooking, RescheduleBooking, CancelBooking, AllocateStation, ApproveResult, ProcessRefund }

public enum OperationStatus { Pending, Running, WaitingExternal, Succeeded, Failed, NeedsAction, ManualReview }

public enum TargetEntityType { BookingAttempt, Reservation, Appointment, BillingItem, QueueTicket, CLSResult }
