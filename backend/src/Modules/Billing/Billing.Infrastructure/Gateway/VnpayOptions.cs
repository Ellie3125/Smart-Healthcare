namespace Billing.Infrastructure.Gateway;

/// <summary>
/// Cấu hình section "Billing:Vnpay" (docs/operations/payment-sandbox.md). TmnCode và HashSecret là secret theo môi trường:
/// dev đặt bằng user-secrets của Api, Staging/Prod bằng biến môi trường Billing__Vnpay__HashSecret; không ghi vào appsettings.
/// </summary>
public sealed class VnpayOptions
{
    public const string Section = "Billing:Vnpay";

    /// <summary>Mã website của bệnh viện tại VNPAY; kiểm lại trong mọi IPN (BR-002: đúng người nhận).</summary>
    public string TmnCode { get; set; } = "";

    public string HashSecret { get; set; } = "";

    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Nơi trình duyệt quay về sau khi thanh toán. Không phải bằng chứng thanh toán: chỉ IPN mới ghi nhận kết quả.</summary>
    public string ReturnUrl { get; set; } = "";

    internal string? Problem() =>
        string.IsNullOrWhiteSpace(TmnCode) ? "thiếu Billing:Vnpay:TmnCode"
        : string.IsNullOrWhiteSpace(HashSecret) ? "thiếu Billing:Vnpay:HashSecret"
        : string.IsNullOrWhiteSpace(ReturnUrl) ? "thiếu Billing:Vnpay:ReturnUrl"
        : null;
}
