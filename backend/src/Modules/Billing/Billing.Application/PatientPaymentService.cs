using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using IdentityPatients.Contracts;

namespace Billing.Application;

/// <summary>
/// Trạng thái yêu cầu thanh toán cho web bệnh nhân. Web hỏi lại API này sau khi VNPAY chuyển về Return URL;
/// trạng thái chỉ đổi khi Billing đã xác minh IPN (hoặc querydr), không theo tham số trên Return URL.
/// </summary>
/// <param name="PaidAtUtc">Thời điểm Billing xác minh đã trả; null khi chưa trả.</param>
/// <param name="CanPay">Còn tạo được liên kết thanh toán: đang chờ, chưa quá hạn, trả qua VNPAY.</param>
public sealed record PatientPaymentStatus(
    Guid PaymentIntentId,
    string IntentNumber,
    PaymentPurpose Purpose,
    decimal Amount,
    string Currency,
    PaymentState Status,
    DateTime ExpiresAtUtc,
    DateTime? PaidAtUtc,
    bool CanPay);

/// <summary>
/// Thao tác thanh toán của bệnh nhân trên kênh Patient. Chỉ người trả của yêu cầu, khi vẫn còn quyền đặt lịch
/// trên hồ sơ bệnh nhân (MED-05), mới thấy hoặc thanh toán được; người khác nhận kết quả như không tồn tại.
/// </summary>
public sealed class PatientPaymentService(
    IPaymentStore store, PaymentCheckoutService checkout, IIdentityPatientsModule identity, TimeProvider clock)
{
    /// <returns>null khi không có hoặc người gọi không có quyền.</returns>
    public async Task<PatientPaymentStatus?> GetAsync(Guid paymentIntentId, Guid userId, CancellationToken ct)
    {
        if (await FindOwnedAsync(paymentIntentId, userId, ct) is not { } found)
            return null;

        var (intent, transaction) = found;

        return new PatientPaymentStatus(
            intent.Id,
            intent.IntentNumber,
            PaymentService.PurposeOf(intent),
            transaction.TransactionAmount,
            transaction.Currency,
            PaymentService.StateOf(intent),
            intent.ExpiresAtUtc,
            transaction.PaymentStatus == PaymentStatus.Success ? transaction.VerifiedAtUtc : null,
            PaymentCheckoutService.CanStart(intent, transaction, clock.GetUtcNow().UtcDateTime));
    }

    /// <summary>Tạo liên kết thanh toán VNPAY. Không ghi gì vào DB nên gọi lại được (ví dụ khi người dùng tải lại trang).</summary>
    /// <returns>null khi không có hoặc người gọi không có quyền.</returns>
    /// <exception cref="BillingException">PaymentConflict, InvalidRequest, GatewayUnavailable như <see cref="PaymentCheckoutService.StartAsync"/>.</exception>
    public async Task<PaymentCheckout?> StartCheckoutAsync(Guid paymentIntentId, Guid userId, string? customerIp, CancellationToken ct)
    {
        if (await FindOwnedAsync(paymentIntentId, userId, ct) is null)
            return null;

        return await checkout.StartAsync(paymentIntentId, customerIp, ct);
    }

    private async Task<(PaymentIntent Intent, PaymentTransaction Transaction)?> FindOwnedAsync(Guid paymentIntentId, Guid userId, CancellationToken ct)
    {
        if (await store.FindPaymentAsync(paymentIntentId, ct) is not { } found || found.Intent.PayerAppUserId != userId)
            return null;

        return await identity.CanAccessPatientAsync(userId, found.Intent.PatientId, AccessLevel.Booking, ct) ? found : null;
    }
}
