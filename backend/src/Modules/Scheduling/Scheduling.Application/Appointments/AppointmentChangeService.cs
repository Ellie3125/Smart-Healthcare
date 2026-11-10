using Billing.Contracts;
using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Scheduling.Application.Appointments;

/// <summary>
/// Hủy và đổi lịch (Phạm vi §6.1, §6.2; Giải pháp v2.13 §10). Mỗi lệnh: idempotency + Operation + tác dụng + sự kiện trong
/// một transaction, mốc 05:00 so bằng giờ DB đọc sau khi nhận khóa. Bên gọi (endpoint) đã kiểm AppUser đại diện PatientId
/// hoặc là tiếp tân, và lấy TargetDoctorRankId từ danh mục phía server.
/// </summary>
public sealed class AppointmentChangeService(IAppointmentChangeStore store, IBillingModule billing, IDbClock dbClock, IUnitOfWork uow,
    IIdempotencyStore idempotency, IOperationTracker operations, IIntegrationEventPublisher publisher)
{
    public const string CancelOperationType = "Scheduling.CancelAppointment";
    public const string RescheduleOperationType = "Scheduling.RescheduleAppointment";

    public Task<ChangeOutcome> CancelAsync(CancelRequest r, CancellationToken ct)
    {
        if (r.AppointmentId == Guid.Empty || r.AppUserId == Guid.Empty || !Idempotent.IsValidKey(r.IdempotencyKey)
            || r.CancelledBy is not (CancellationInitiator.Patient or CancellationInitiator.Receptionist)
            || (r.CancelledBy == CancellationInitiator.Patient && (r.PatientId ?? Guid.Empty) == Guid.Empty)
            || r.Reason is { Length: > 500 })
            return Task.FromResult(new ChangeOutcome(ChangeResult.InvalidInput));

        var hash = Idempotent.Hash(r.AppointmentId, r.CancelledBy, r.PatientId?.ToString() ?? "", r.Reason ?? "",
            Convert.ToHexString(r.ExpectedRowVersion ?? []));
        return RunAsync(r.AppUserId, CancelOperationType, r.IdempotencyKey, hash, OperationType.CancelBooking, r.AppointmentId,
            async (operationId, t) =>
            {
                var outcome = await store.CancelAsync(r, operationId, t);
                if (outcome is { Result: ChangeResult.Cancelled, Cancelled: { } c })
                {
                    await publisher.PublishAsync(new AppointmentCancelled(Guid.CreateVersion7(), r.AppointmentId, c.BookingAttemptId,
                        c.PatientId, c.DoctorId, c.ServiceDate, r.CancelledBy, c.SlotReleased ? ConsultationRefund.Full : ConsultationRefund.None,
                        c.SlotReleased, c.CancelledAtUtc, c.RescheduleCutoffTimeUtc, c.CancelledAtUtc, operationId), r.AppointmentId, t);
                }

                return outcome;
            }, ct);
    }

    public Task<ChangeOutcome> RescheduleAsync(RescheduleRequest r, CancellationToken ct)
    {
        if (r.AppointmentId == Guid.Empty || r.AppUserId == Guid.Empty || r.PatientId == Guid.Empty
            || r.TargetDoctorId == Guid.Empty || r.TargetDoctorRankId == Guid.Empty || r.TargetServiceDate == DateOnly.MinValue
            || !Idempotent.IsValidKey(r.IdempotencyKey))
            return Task.FromResult(new ChangeOutcome(ChangeResult.InvalidInput));

        var hash = Idempotent.Hash(r.AppointmentId, r.PatientId, r.TargetDoctorId, r.TargetDoctorRankId,
            r.TargetServiceDate.ToString("yyyy-MM-dd"), Convert.ToHexString(r.ExpectedRowVersion ?? []));
        return RunAsync(r.AppUserId, RescheduleOperationType, r.IdempotencyKey, hash, OperationType.RescheduleBooking, r.AppointmentId,
            async (operationId, t) =>
            {
                // Giá đang hiệu lực của bác sĩ đích (Giải pháp §10.1); gọi trước khi khóa để không giữ khóa trong lúc báo giá.
                var targetFee = (await billing.QuoteExamFeeAsync(
                    new QuoteExamFee(r.TargetDoctorId, r.TargetDoctorRankId, await dbClock.GetUtcNowAsync(t)), t)).UnitPrice;
                var outcome = await store.RescheduleAsync(r, targetFee, t);
                if (outcome is { Result: ChangeResult.Rescheduled, Rescheduled: { } n })
                {
                    await publisher.PublishAsync(new AppointmentConfirmed(Guid.CreateVersion7(), n.AppointmentId, n.BookingAttemptId,
                        r.PatientId, r.TargetDoctorId, r.TargetServiceDate, n.AppointmentNumber, r.AppointmentId, n.ConfirmedAtUtc,
                        operationId), n.AppointmentId, t);
                }

                return outcome;
            }, ct);
    }

    private Task<ChangeOutcome> RunAsync(Guid appUserId, string idempotencyType, string key, string hash, OperationType operationType,
        Guid appointmentId, Func<Guid, CancellationToken, Task<ChangeOutcome>> work, CancellationToken ct) =>
        uow.ExecuteAsync(async t =>
        {
            var scope = Idempotent.Scope(appUserId);
            var operationId = Guid.CreateVersion7();
            switch (await idempotency.BeginAsync(scope, idempotencyType, key, hash, Idempotent.Retention, operationId, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return Idempotent.Deserialize<ChangeOutcome>(replay.BodyJson);
                case IdempotencyBeginResult.InProgress inProgress:
                    return new ChangeOutcome(ChangeResult.InProgress, OperationId: inProgress.OperationId);
                case IdempotencyBeginResult.Conflict:
                    return new ChangeOutcome(ChangeResult.IdempotencyConflict);
            }

            await operations.StartAsync(new StartOperation(operationId, appUserId, operationType, TargetEntityType.Appointment,
                appointmentId, null), t);
            var outcome = await work(operationId, t) with { OperationId = operationId };
            var done = outcome.Result is ChangeResult.Cancelled or ChangeResult.Rescheduled;
            await operations.CompleteAsync(operationId, done ? OperationStatus.Succeeded : OperationStatus.NeedsAction,
                done ? null : outcome.Result.ToString(), t);
            await idempotency.CompleteAsync(scope, idempotencyType, key, StatusCode(outcome.Result), Idempotent.Serialize(outcome),
                operationId, t);
            return outcome;
        }, ct);

    public static int StatusCode(ChangeResult result) => result switch
    {
        ChangeResult.Cancelled or ChangeResult.Rescheduled => 200,
        ChangeResult.InvalidInput or ChangeResult.ReasonRequired => 400,
        ChangeResult.AppointmentNotFound or ChangeResult.DayNotFound => 404,
        _ => 409,
    };
}

/// <param name="PatientId">Bắt buộc khi bệnh nhân hủy: hồ sơ mà người gọi đại diện.</param>
/// <param name="Reason">Bắt buộc khi tiếp tân hủy lịch đã check in (≤ 500 ký tự, không ghi CCCD/số điện thoại).</param>
/// <param name="ExpectedRowVersion">Phiên bản lịch hẹn người gọi đã đọc; khác thì VersionConflict. Null thì không kiểm.</param>
public sealed record CancelRequest(Guid AppointmentId, Guid AppUserId, CancellationInitiator CancelledBy, Guid? PatientId,
    string? Reason, byte[]? ExpectedRowVersion, string IdempotencyKey);

public sealed record RescheduleRequest(Guid AppointmentId, Guid AppUserId, Guid PatientId, Guid TargetDoctorId, Guid TargetDoctorRankId,
    DateOnly TargetServiceDate, byte[]? ExpectedRowVersion, string IdempotencyKey);

public enum ChangeResult
{
    Cancelled,
    Rescheduled,
    InvalidInput,
    AppointmentNotFound,
    /// <summary>Trạng thái lịch không cho phép (vai trò này) hủy/đổi.</summary>
    InvalidAppointmentState,
    /// <summary>Tiếp tân hủy lịch đã check in mà không có lý do.</summary>
    ReasonRequired,
    VersionConflict,
    /// <summary>Đổi lịch từ đúng 05:00 ngày D cũ (giờ DB).</summary>
    RescheduleCutoffPassed,
    /// <summary>Bác sĩ đích khác mức phí áp dụng: hủy rồi đặt mới.</summary>
    DifferentFee,
    /// <summary>Đích trùng bác sĩ và ngày của lịch hiện tại.</summary>
    SameDay,
    DayNotFound,
    DayClosed,
    CutoffPassed,
    Full,
    AlreadyClaimed,
    InProgress,
    IdempotencyConflict,
}

public sealed record CancelledInfo(Guid BookingAttemptId, Guid PatientId, Guid DoctorId, DateOnly ServiceDate, bool SlotReleased,
    DateTime CancelledAtUtc, DateTime RescheduleCutoffTimeUtc);

public sealed record RescheduledInfo(Guid AppointmentId, string AppointmentNumber, Guid BookingAttemptId, DateTime ConfirmedAtUtc);

/// <summary>Refund: Full khi hủy trước 05:00, None từ 05:00 (Scheduling quyết định, Billing không tính lại).</summary>
public sealed record ChangeOutcome(ChangeResult Result, CancelledInfo? Cancelled = null, RescheduledInfo? Rescheduled = null,
    Guid? OperationId = null);

/// <summary>Chạy bên trong <see cref="IUnitOfWork"/>.</summary>
public interface IAppointmentChangeStore
{
    Task<ChangeOutcome> CancelAsync(CancelRequest request, Guid operationId, CancellationToken ct);

    Task<ChangeOutcome> RescheduleAsync(RescheduleRequest request, decimal targetFee, CancellationToken ct);
}
