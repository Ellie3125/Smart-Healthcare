using Billing.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.BookingAttempts;

/// <summary>
/// Tạo yêu cầu thanh toán phí khám cho suất đang giữ (Giải pháp v2.14 §5.4 bước 1): khoản thu phí khám + PaymentIntent
/// của Billing và chuyển BookingAttempt sang WaitingPayment trong cùng unit of work (host đăng ký BillingDbContext làm
/// participant). Mỗi lần giữ chỗ chỉ có một yêu cầu: gọi lại trả đúng yêu cầu đã tạo, không sinh khoản thu thứ hai.
/// Bên gọi (endpoint) phải kiểm quyền Booking của AppUser trên hồ sơ trước khi gọi.
/// </summary>
public sealed class BookingPaymentService(IBookingPaymentStore store, IBillingModule billing, IUnitOfWork uow,
    IIdempotencyStore idempotency, IDbClock dbClock)
{
    public const string OperationType = "Scheduling.StartBookingPayment";

    public Task<BookingPaymentOutcome> StartAsync(Guid bookingAttemptId, Guid appUserId,
        Func<Guid, CancellationToken, Task<string?>> resolveDoctorNameAsync, CancellationToken ct)
    {
        if (bookingAttemptId == Guid.Empty || appUserId == Guid.Empty)
            return Task.FromResult(new BookingPaymentOutcome(BookingPaymentResult.AttemptNotFound));

        return uow.ExecuteAsync(async t =>
        {
            var hold = await store.GetLatestHoldAsync(bookingAttemptId, appUserId, t);
            if (hold is null)
                return new BookingPaymentOutcome(BookingPaymentResult.AttemptNotFound);
            if (hold.AttemptStatus is not (BookingAttemptStatus.Held or BookingAttemptStatus.WaitingPayment))
                return new BookingPaymentOutcome(BookingPaymentResult.InvalidAttemptState);
            if (hold.ReservationStatus != ReservationStatus.Held || hold.ExpiresAtUtc <= await dbClock.GetUtcNowAsync(t))
                return new BookingPaymentOutcome(BookingPaymentResult.HoldExpired);

            // Khóa idempotency là ReservationId (không nhận từ client): một lần giữ chỗ, một yêu cầu thanh toán.
            var scope = Idempotent.Scope(appUserId);
            var key = hold.ReservationId.ToString("N");
            switch (await idempotency.BeginAsync(scope, OperationType, key, Idempotent.Hash(bookingAttemptId), Idempotent.Retention, null, t))
            {
                case IdempotencyBeginResult.Replay replay:
                    return Idempotent.Deserialize<BookingPaymentOutcome>(replay.BodyJson);
                case IdempotencyBeginResult.InProgress:
                case IdempotencyBeginResult.Conflict:
                    return new BookingPaymentOutcome(BookingPaymentResult.InProgress);
            }

            var doctorName = await resolveDoctorNameAsync(hold.DoctorId, t);
            if (doctorName is null)
                return new BookingPaymentOutcome(BookingPaymentResult.DoctorNotFound);

            // Áp giá tại lúc giữ chỗ, cùng mốc với phí PaymentVerifiedHandler ghi vào Appointment (W2-06).
            var charge = await billing.CreateExamChargeAsync(
                new CreateExamCharge(hold.DoctorId, hold.DoctorRankId, ItemName(doctorName), hold.ReservedAtUtc), t);
            var payment = await billing.CreatePaymentAsync(new CreatePayment(PaymentPurpose.InitialConsultation, hold.PatientId,
                appUserId, bookingAttemptId, null, [charge.ChargeId], PaymentProvider.VNPAY, hold.ExpiresAtUtc), t);
            await store.MarkWaitingPaymentAsync(bookingAttemptId, t);

            var outcome = new BookingPaymentOutcome(BookingPaymentResult.Started, payment.PaymentIntentId, payment.Amount,
                payment.Currency, payment.ExpiresAtUtc);
            await idempotency.CompleteAsync(scope, OperationType, key, 200, Idempotent.Serialize(outcome), null, t);
            return outcome;
        }, ct);
    }

    private static string ItemName(string doctorName)
    {
        var name = $"Phí khám - {doctorName}";
        return name.Length <= 200 ? name : name[..200];
    }
}

public enum BookingPaymentResult
{
    Started,
    /// <summary>Không có BookingAttempt của người gọi, hoặc chưa giữ chỗ lần nào.</summary>
    AttemptNotFound,
    /// <summary>BookingAttempt không ở Held/WaitingPayment.</summary>
    InvalidAttemptState,
    /// <summary>Giữ chỗ mới nhất không còn Held hoặc đã quá ExpiresAtUtc theo giờ DB.</summary>
    HoldExpired,
    DoctorNotFound,
    /// <summary>Một yêu cầu khác cho cùng lần giữ chỗ đang chạy.</summary>
    InProgress,
}

public sealed record BookingPaymentOutcome(BookingPaymentResult Result, Guid? PaymentIntentId = null, decimal? Amount = null,
    string? Currency = null, DateTime? ExpiresAtUtc = null);

public sealed record HeldBooking(Guid PatientId, BookingAttemptStatus AttemptStatus, Guid DoctorId, Guid DoctorRankId,
    Guid ReservationId, ReservationStatus ReservationStatus, DateTime ReservedAtUtc, DateTime ExpiresAtUtc);

/// <summary>Các method chạy bên trong <see cref="IUnitOfWork"/>.</summary>
public interface IBookingPaymentStore
{
    /// <summary>BookingAttempt của người gọi kèm lần giữ chỗ mới nhất; null khi không có attempt, chưa chọn bác sĩ hoặc chưa giữ chỗ.</summary>
    Task<HeldBooking?> GetLatestHoldAsync(Guid bookingAttemptId, Guid appUserId, CancellationToken ct);

    Task MarkWaitingPaymentAsync(Guid bookingAttemptId, CancellationToken ct);
}
