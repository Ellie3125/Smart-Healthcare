using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>Liên kết thanh toán để app mở trang cổng. App hỏi kết quả qua API của Backend, không tin trang cổng chuyển về.</summary>
public sealed record PaymentCheckout(
    Guid PaymentIntentId,
    string TransactionReference,
    string PaymentUrl,
    decimal Amount,
    string Currency,
    DateTime ExpiresAtUtc);

/// <summary>
/// Bước gọi cổng sau khi yêu cầu thanh toán đã commit. Tách khỏi
/// <see cref="IBillingModule.CreatePaymentAsync"/> để module khác vẫn tạo yêu cầu được trong unit of work của họ mà không
/// kéo lời gọi mạng vào transaction (MOD-05). Gọi lại được: chưa ghi gì vào DB cho tới khi kết quả về.
/// </summary>
public sealed class PaymentCheckoutService(IPaymentStore store, IPaymentGateway gateway, TimeProvider clock)
{
    /// <param name="customerIp">IP của người trả, nếu biết.</param>
    public async Task<PaymentCheckout> StartAsync(Guid paymentIntentId, string? customerIp, CancellationToken ct)
    {
        if (store.HasActiveTransaction)
            throw new InvalidOperationException("Không gọi cổng thanh toán bên trong transaction DB (MOD-05); gọi sau khi commit.");

        var found = await store.FindPaymentAsync(paymentIntentId, ct)
            ?? throw new BillingException(BillingErrorCode.PaymentNotFound, $"Không có yêu cầu thanh toán {paymentIntentId}.");
        var (intent, transaction) = found;
        EnsureOpen(intent, transaction, clock.GetUtcNow().UtcDateTime);

        GatewayCheckout checkout;
        try
        {
            checkout = await gateway.CreateCheckoutAsync(new GatewayCheckoutRequest(
                transaction.TransactionReference,
                transaction.TransactionAmount,
                transaction.Currency,
                Describe(intent),
                intent.ExpiresAtUtc,
                customerIp), ct);
        }
        catch (PaymentGatewayException ex)
        {
            throw new BillingException(BillingErrorCode.GatewayUnavailable,
                $"Chưa tạo được liên kết thanh toán cho {intent.IntentNumber}: {ex.Message} Yêu cầu vẫn giữ, có thể thử lại.");
        }

        return new PaymentCheckout(intent.Id, transaction.TransactionReference, checkout.PaymentUrl,
            transaction.TransactionAmount, transaction.Currency, intent.ExpiresAtUtc);
    }

    private static void EnsureOpen(PaymentIntent intent, PaymentTransaction transaction, DateTime nowUtc)
    {
        if (intent.Provider != PaymentIntentProvider.VNPAY)
            throw new BillingException(BillingErrorCode.InvalidRequest, $"Yêu cầu {intent.IntentNumber} dùng {intent.Provider}; hiện chỉ VNPAY tạo được liên kết thanh toán.");
        if (intent.Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.Processing) || transaction.PaymentStatus != PaymentStatus.Pending)
            throw new BillingException(BillingErrorCode.PaymentConflict, $"Yêu cầu {intent.IntentNumber} không còn chờ thanh toán ({intent.Status}).");
        if (intent.ExpiresAtUtc <= nowUtc)
            throw new BillingException(BillingErrorCode.PaymentConflict, $"Yêu cầu {intent.IntentNumber} đã quá hạn; tạo yêu cầu mới.");
    }

    private static string Describe(PaymentIntent intent) =>
        (intent.PaymentType == PaymentType.InitialConsultation ? "Thanh toan phi kham ban dau " : "Thanh toan phi can lam sang ") + intent.IntentNumber;
}
