using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>Yêu cầu thanh toán còn mở: intent Pending/Processing chưa hết hạn, có giao dịch Pending.</summary>
public sealed record OpenPayment(PaymentIntent Intent, PaymentTransaction Transaction, IReadOnlyList<Guid> ChargeIds);

/// <summary>
/// Lưu trữ thanh toán. Mọi bước đọc-kiểm-ghi chạy trong <see cref="InTransactionAsync{T}"/>; các hàm Lock* giữ khóa
/// cập nhật tới cuối transaction (DB-08) để hai backend không cùng tạo/ghi nhận một thanh toán.
/// </summary>
public interface IPaymentStore
{
    /// <summary>Chạy trong transaction (đã có transaction thì tham gia transaction đó); deadlock thì chạy lại toàn bộ (DB-12).</summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

    /// <summary>Đang có transaction mở trên kết nối của Billing (của bên gọi hoặc của chính Billing).</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Đọc (không khóa) intent và giao dịch của một yêu cầu thanh toán.</summary>
    Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindPaymentAsync(Guid paymentIntentId, CancellationToken ct);

    /// <summary>Khóa khoản thu theo thứ tự Id cố định (DB-09); trả các khoản tìm thấy.</summary>
    Task<IReadOnlyList<BillingItem>> LockChargesAsync(IReadOnlyList<Guid> chargeIds, CancellationToken ct);

    /// <summary>Khóa phạm vi yêu cầu thanh toán của một BookingAttempt để hai request không cùng tạo.</summary>
    Task LockBookingAttemptAsync(Guid bookingAttemptId, CancellationToken ct);

    Task<IReadOnlyList<OpenPayment>> FindOpenPaymentsAsync(IReadOnlyCollection<Guid> chargeIds, Guid? bookingAttemptId, DateTime nowUtc, CancellationToken ct);

    /// <summary>Thêm intent, giao dịch và phân bổ; store gán khóa ngoại giữa chúng.</summary>
    void Add(PaymentIntent intent, PaymentTransaction transaction, IReadOnlyList<PaymentAllocation> allocations);

    Task<PaymentTransaction?> LockTransactionAsync(string transactionReference, CancellationToken ct);

    /// <summary>
    /// Mã giao dịch của cổng đã được ghi cho giao dịch khác chưa (UK có lọc trên ProviderTransactionId).
    /// Giữ khóa phạm vi trên mã đó tới cuối transaction để hai IPN mang cùng mã không cùng ghi (DB-08).
    /// </summary>
    Task<bool> IsProviderTransactionIdTakenAsync(string providerTransactionId, Guid exceptTransactionId, CancellationToken ct);

    Task<PaymentIntent> GetIntentAsync(Guid paymentIntentId, CancellationToken ct);

    /// <summary>Kết quả cổng đã nhận theo (Provider, EventId) chưa; dùng chống xử lý lặp callback (BR-002).</summary>
    Task<bool> HasEventAsync(PaymentEventProvider provider, string eventId, CancellationToken ct);

    void AddEvent(PaymentEvent paymentEvent);

    Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Ghi sự kiện vào Outbox trong cùng transaction với thay đổi nghiệp vụ (MSG-02).</summary>
    Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct);
}

/// <summary>Kết quả cổng thanh toán đã được adapter xác minh chữ ký, người nhận và mã tham chiếu (BR-002).</summary>
/// <param name="ProviderEventId">Mã sự kiện của cổng; cùng một kết quả gửi lại luôn mang cùng mã (PaymentEvent.EventId).</param>
/// <param name="ProviderRawData">Nội dung thô cổng gửi (đã xác minh chữ ký), lưu để đối soát.</param>
public sealed record VerifiedPayment(
    string TransactionReference,
    string ProviderTransactionId,
    string ProviderEventId,
    decimal Amount,
    string Currency,
    DateTime VerifiedAtUtc,
    string? ProviderRawData);

/// <param name="AlreadyRecorded">Callback lặp: giao dịch đã ghi nhận trước đó, không đổi gì và không phát sự kiện mới.</param>
public sealed record PaymentConfirmation(Guid PaymentIntentId, bool AlreadyRecorded, IReadOnlyList<Guid> PaidChargeIds);

/// <summary>Cổng báo giao dịch thất bại. Đã xác minh chữ ký và người nhận.</summary>
/// <param name="ProviderTransactionId">Mã giao dịch của cổng; null khi cổng chưa phát sinh giao dịch (ví dụ người trả hủy).</param>
public sealed record FailedPayment(
    string TransactionReference,
    string? ProviderTransactionId,
    string ProviderEventId,
    DateTime FailedAtUtc,
    string? ProviderRawData);

public enum PaymentFailureOutcome
{
    /// <summary>Giao dịch và yêu cầu chuyển Failed; bệnh nhân tạo yêu cầu mới nếu muốn trả tiếp.</summary>
    Recorded,

    /// <summary>Đã ghi Failed từ trước; không đổi gì.</summary>
    AlreadyRecorded,

    /// <summary>Giao dịch đã Paid: kết quả thất bại cũ không ghi đè (BR-002, MSG-03).</summary>
    IgnoredAlreadyPaid,
}

public sealed record PaymentFailure(Guid PaymentIntentId, PaymentFailureOutcome Outcome);
