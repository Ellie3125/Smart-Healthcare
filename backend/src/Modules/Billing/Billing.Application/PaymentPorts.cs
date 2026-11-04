using Billing.Contracts;
using Billing.Domain.Entities;

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

    Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Ghi sự kiện vào Outbox trong cùng transaction với thay đổi nghiệp vụ (MSG-02).</summary>
    Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct);
}

/// <summary>Kết quả cổng thanh toán đã được adapter xác minh chữ ký, người nhận và mã tham chiếu (BR-002).</summary>
public sealed record VerifiedPayment(
    string TransactionReference,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    DateTime VerifiedAtUtc,
    string? ProviderRawData);

/// <param name="AlreadyRecorded">IPN lặp: giao dịch đã ghi nhận trước đó, không đổi gì và không phát sự kiện mới.</param>
public sealed record PaymentConfirmation(Guid PaymentIntentId, bool AlreadyRecorded, IReadOnlyList<Guid> PaidChargeIds);
