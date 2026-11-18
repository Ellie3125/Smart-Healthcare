using System.Globalization;
using System.Text.Json;
using Billing.Application;
using Billing.Contracts;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Gateway;

/// <summary>
/// IPN của VNPAY. Thứ tự kiểm: chữ ký → người nhận (TmnCode) → giao PaymentService kiểm mã tham chiếu, số tiền và chống lặp
/// (BR-002). Không tin bất kỳ tham số nào trước khi chữ ký đúng.
/// </summary>
internal sealed class VnpayIpnHandler(PaymentService payments, IOptions<VnpayOptions> options, TimeProvider clock) : IPaymentCallbackHandler
{
    private const string Success = "00";
    private const string NotCompleted = "01";

    // VNPAY gửi vnp_TransactionNo = 0 cho giao dịch chưa phát sinh ở ngân hàng (ví dụ người trả hủy).
    private const string NoTransaction = "0";

    public async Task<PaymentCallbackOutcome> HandleAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        var settings = options.Value;
        if (settings.Problem() is { } problem)
            throw new BillingException(BillingErrorCode.GatewayUnavailable, $"VNPAY chưa cấu hình: {problem}.");
        if (!VnpaySignature.Verify(settings.HashSecret, parameters))
            throw new BillingException(BillingErrorCode.InvalidSignature, "Chữ ký IPN không hợp lệ.");
        if (Find(parameters, "vnp_TmnCode") != settings.TmnCode)
            throw new BillingException(BillingErrorCode.InvalidRequest, "IPN gửi cho mã website khác.");

        var reference = Required(parameters, "vnp_TxnRef");
        var responseCode = Required(parameters, "vnp_ResponseCode");
        var transactionStatus = Required(parameters, "vnp_TransactionStatus");
        var transactionNo = Find(parameters, "vnp_TransactionNo");
        var completedAtUtc = VnpayTime.ParseUtc(Find(parameters, "vnp_PayDate")) ?? clock.GetUtcNow().UtcDateTime;
        var raw = JsonSerializer.Serialize(new SortedDictionary<string, string>(parameters.ToDictionary(), StringComparer.Ordinal));

        // Chỉ ghi nhận đã trả khi cả hai mã đều thành công.
        if (responseCode == Success && transactionStatus == Success)
        {
            if (string.IsNullOrEmpty(transactionNo) || transactionNo == NoTransaction)
                throw new BillingException(BillingErrorCode.InvalidRequest, "IPN thành công nhưng không có mã giao dịch VNPAY.");

            var confirmed = await payments.ConfirmVerifiedAsync(new VerifiedPayment(
                reference, transactionNo, transactionNo, Amount(parameters), "VND", completedAtUtc, raw), ct);
            return confirmed.AlreadyRecorded ? PaymentCallbackOutcome.PaymentAlreadyRecorded : PaymentCallbackOutcome.PaymentRecorded;
        }

        if (transactionStatus == NotCompleted)
            throw new BillingException(BillingErrorCode.InvalidRequest, $"Giao dịch {reference} chưa có kết quả cuối.");

        var providerTransactionId = string.IsNullOrEmpty(transactionNo) || transactionNo == NoTransaction ? null : transactionNo;
        var failed = await payments.RecordFailedAsync(new FailedPayment(
            reference, providerTransactionId, $"{reference}-{responseCode}", Amount(parameters), "VND", completedAtUtc, raw), ct);
        return failed.Outcome switch
        {
            PaymentFailureOutcome.Recorded => PaymentCallbackOutcome.FailureRecorded,
            PaymentFailureOutcome.AlreadyRecorded => PaymentCallbackOutcome.FailureAlreadyRecorded,
            _ => PaymentCallbackOutcome.FailureIgnoredAlreadyPaid,
        };
    }

    // VNPAY gửi số tiền nhân 100.
    private static decimal Amount(IReadOnlyDictionary<string, string> parameters) =>
        decimal.TryParse(Required(parameters, "vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            ? amount / 100m
            : throw new BillingException(BillingErrorCode.InvalidRequest, "vnp_Amount không đúng định dạng.");

    private static string? Find(IReadOnlyDictionary<string, string> parameters, string name) => parameters.GetValueOrDefault(name);

    private static string Required(IReadOnlyDictionary<string, string> parameters, string name) =>
        Find(parameters, name) is { Length: > 0 } value ? value : throw new BillingException(BillingErrorCode.InvalidRequest, $"IPN thiếu {name}.");
}
