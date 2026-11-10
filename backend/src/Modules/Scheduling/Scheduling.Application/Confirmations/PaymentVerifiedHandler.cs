using Billing.Contracts;
using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Scheduling.Application.Confirmations;

/// <summary>
/// PaymentVerified (phí khám ban đầu) → xác nhận lịch (Giải pháp v2.14 §5.1, §5.2). Một transaction: đánh dấu ProcessedEvent,
/// khóa quota rồi reservation, tạo Appointment hoặc ghi PaidUnallocated, phát AppointmentConfirmed qua Outbox.
/// Dispatcher Outbox gọi handler (W3-00); lỗi ném ra thì transaction rollback và sự kiện được giao lại.
/// </summary>
public sealed class PaymentVerifiedHandler(IConfirmationStore store, IBillingModule billing, IUnitOfWork uow,
    IProcessedEventStore processed, IIntegrationEventPublisher publisher, IOperationTracker operations)
    : IIntegrationEventHandler<PaymentVerified>
{
    public const string HandlerName = "Scheduling.PaymentVerified.ConfirmAppointment";

    public Task HandleAsync(PaymentVerified e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Phí CLS (CLSOrderBatch) thuộc luồng vé CLS (W3), không phải handler này.
        if (e.Purpose != PaymentPurpose.InitialConsultation || e.BookingAttemptId is not { } attemptId)
            return Task.CompletedTask;

        return uow.ExecuteAsync(async t =>
        {
            if (!await processed.TryMarkAsync(e.EventId, HandlerName, t))
                return;

            var candidate = await store.FindCandidateAsync(attemptId, t);
            // shortcut: phí áp dụng được báo giá lại tại ReservedAtUtc (cùng mốc Billing áp giá khoản phí khám), không đọc số tiền
            // đã thu; đổi sang số tiền của khoản đã thu khi IBillingModule có API tra theo BookingAttemptId.
            var fee = candidate is null ? 0m : (await billing.QuoteExamFeeAsync(
                new QuoteExamFee(candidate.DoctorId, candidate.DoctorRankId, candidate.ReservedAtUtc), t)).UnitPrice;

            var result = candidate is null
                ? new ConfirmResult.PaidUnallocated("NoReservation")
                : await store.ConfirmAsync(candidate.ReservationId, fee, t);

            switch (result)
            {
                case ConfirmResult.Confirmed c:
                    await publisher.PublishAsync(new AppointmentConfirmed(Guid.CreateVersion7(), c.AppointmentId, attemptId,
                        c.PatientId, c.DoctorId, c.ServiceDate, c.AppointmentNumber, null, c.ConfirmedAtUtc, e.OperationId),
                        c.AppointmentId, t);
                    break;
                case ConfirmResult.PaidUnallocated late:
                    await store.MarkPaidUnallocatedAsync(attemptId, t);
                    // Ngoại lệ cho màn hình xử lý (W4-04); hoàn tiền do Billing (PROGRESS mục 5).
                    var operationId = Guid.CreateVersion7();
                    await operations.StartAsync(new StartOperation(operationId, candidate?.AppUserId ?? Guid.Empty,
                        OperationType.ConfirmBooking, TargetEntityType.BookingAttempt, attemptId, attemptId), t);
                    await operations.CompleteAsync(operationId, OperationStatus.ManualReview,
                        $"PaidUnallocated: {late.Reason}; PaymentIntentId {e.PaymentIntentId:D}", t);
                    break;
            }
        }, ct);
    }
}

/// <summary>Reservation gắn với BookingAttempt: ưu tiên bản Held mới nhất, nếu không có thì bản mới nhất.</summary>
public sealed record ConfirmCandidate(Guid ReservationId, Guid AppUserId, Guid DoctorId, Guid DoctorRankId, DateTime ReservedAtUtc);

public abstract record ConfirmResult
{
    private ConfirmResult() { }

    public sealed record Confirmed(Guid AppointmentId, string AppointmentNumber, Guid PatientId, Guid DoctorId, DateOnly ServiceDate,
        DateTime ConfirmedAtUtc) : ConfirmResult;

    /// <summary>Reservation đã xác nhận trước đó (sự kiện khác cùng giao dịch): không làm gì.</summary>
    public sealed record AlreadyConfirmed : ConfirmResult;

    /// <summary>Đã thu tiền nhưng giữ chỗ không còn hợp lệ (Expired, Cancelled, quá ExpiresAt).</summary>
    public sealed record PaidUnallocated(string Reason) : ConfirmResult;
}

/// <summary>Các method chạy bên trong <see cref="IUnitOfWork"/>.</summary>
public interface IConfirmationStore
{
    Task<ConfirmCandidate?> FindCandidateAsync(Guid bookingAttemptId, CancellationToken ct);

    Task<ConfirmResult> ConfirmAsync(Guid reservationId, decimal appliedFee, CancellationToken ct);

    Task MarkPaidUnallocatedAsync(Guid bookingAttemptId, CancellationToken ct);
}
