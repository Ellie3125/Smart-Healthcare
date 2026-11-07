using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Tests;

/// <summary>Cho các test không được chạm tới lưu trữ thanh toán (ví dụ: lỗi đầu vào phải bị chặn trước khi mở transaction).</summary>
internal sealed class UnusedPaymentStore : IPaymentStore
{
    private static InvalidOperationException Unused() => new("Test không được dùng IPaymentStore.");
    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => throw Unused();
    public bool HasActiveTransaction => throw Unused();
    public Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindPaymentAsync(Guid paymentIntentId, CancellationToken ct) => throw Unused();
    public Task<bool> HasEventAsync(PaymentEventProvider provider, string eventId, CancellationToken ct) => throw Unused();
    public void AddEvent(PaymentEvent paymentEvent) => throw Unused();
    public Task<IReadOnlyList<BillingItem>> LockChargesAsync(IReadOnlyList<Guid> chargeIds, CancellationToken ct) => throw Unused();
    public Task LockBookingAttemptAsync(Guid bookingAttemptId, CancellationToken ct) => throw Unused();
    public Task<IReadOnlyList<OpenPayment>> FindOpenPaymentsAsync(IReadOnlyCollection<Guid> chargeIds, Guid? bookingAttemptId, DateTime nowUtc, CancellationToken ct) => throw Unused();
    public void Add(PaymentIntent intent, PaymentTransaction transaction, IReadOnlyList<PaymentAllocation> allocations) => throw Unused();
    public Task<PaymentTransaction?> LockTransactionAsync(string transactionReference, CancellationToken ct) => throw Unused();
    public Task<bool> IsProviderTransactionIdTakenAsync(string providerTransactionId, Guid exceptTransactionId, CancellationToken ct) => throw Unused();
    public Task<PaymentIntent> GetIntentAsync(Guid paymentIntentId, CancellationToken ct) => throw Unused();
    public Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentTransactionId, CancellationToken ct) => throw Unused();
    public Task SaveChangesAsync(CancellationToken ct) => throw Unused();
    public Task EnqueueAsync(PaymentVerified integrationEvent, CancellationToken ct) => throw Unused();
}
