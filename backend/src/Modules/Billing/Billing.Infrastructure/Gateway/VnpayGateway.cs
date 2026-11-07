using System.Globalization;
using System.Net;
using Billing.Application;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Gateway;

/// <summary>
/// Tạo liên kết thanh toán VNPAY. VNPAY không có API tạo giao dịch: Billing tự dựng URL có ký rồi chuyển người trả sang đó,
/// nên bước này không gọi mạng. Mã giao dịch của VNPAY (vnp_TransactionNo) chỉ có khi IPN về.
/// </summary>
internal sealed class VnpayGateway(IOptions<VnpayOptions> options) : IPaymentGateway
{
    private const string Currency = "VND";
    private const int MinimumIpLength = 7;
    private const string LoopbackIp = "127.0.0.1";

    public Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        if (settings.Problem() is { } problem)
            throw new PaymentGatewayException($"VNPAY chưa cấu hình: {problem}.");
        if (request.Currency != Currency)
            throw new PaymentGatewayException($"VNPAY chỉ nhận {Currency}, không nhận {request.Currency}.");
        if (request.Amount != decimal.Truncate(request.Amount))
            throw new PaymentGatewayException($"Số tiền {request.Amount} không phải số nguyên VND.");

        var parameters = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = settings.TmnCode,
            ["vnp_Amount"] = (request.Amount * 100m).ToString("0", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = Currency,
            ["vnp_TxnRef"] = request.TransactionReference,
            ["vnp_OrderInfo"] = request.Description,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = settings.ReturnUrl,
            ["vnp_IpAddr"] = request.CustomerIp is { Length: >= MinimumIpLength } ip ? ip : LoopbackIp,
            // Từ giá trị đã lưu, không lấy giờ hiện tại: querydr phải tính lại đúng vnp_CreateDate (Giải pháp kỹ thuật v2.13 mục 5.4).
            ["vnp_CreateDate"] = VnpayTime.Format(request.CreatedAtUtc),
            ["vnp_ExpireDate"] = VnpayTime.Format(request.ExpiresAtUtc),
        };

        var query = VnpaySignature.Canonical(parameters);
        var url = $"{settings.PaymentUrl}?{query}&{VnpaySignature.HashParameter}={WebUtility.UrlEncode(VnpaySignature.Sign(settings.HashSecret, query))}";
        return Task.FromResult(new GatewayCheckout(url));
    }
}

/// <summary>VNPAY dùng giờ GMT+7 dạng yyyyMMddHHmmss; Việt Nam không đổi giờ theo mùa.</summary>
internal static class VnpayTime
{
    private const string Format14 = "yyyyMMddHHmmss";
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static string Format(DateTime utc) => (utc + Offset).ToString(Format14, CultureInfo.InvariantCulture);

    public static DateTime? ParseUtc(string? value) =>
        DateTime.TryParseExact(value, Format14, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? DateTime.SpecifyKind(local - Offset, DateTimeKind.Utc)
            : null;
}
