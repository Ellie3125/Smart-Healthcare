namespace Billing.Infrastructure.Gateway;

/// <summary>
/// Cấu hình section "Vnpay" (Giải pháp kỹ thuật v2.13 mục 5.4). TmnCode và HashSecret là secret theo môi trường:
/// dev đặt bằng user-secrets của Api, Staging/Prod bằng biến môi trường Vnpay__TmnCode, Vnpay__HashSecret; không ghi vào appsettings.
/// </summary>
public sealed class VnpayOptions
{
    public const string Section = "Vnpay";

    /// <summary>Mã website của bệnh viện tại VNPAY; kiểm lại trong mọi IPN (BR-002: đúng người nhận).</summary>
    public string TmnCode { get; set; } = "";

    public string HashSecret { get; set; } = "";

    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Nơi trình duyệt quay về sau khi thanh toán. Không phải bằng chứng thanh toán: chỉ IPN mới ghi nhận kết quả.</summary>
    public string ReturnUrl { get; set; } = "";

    internal string? Problem() =>
        string.IsNullOrWhiteSpace(TmnCode) ? "thiếu Vnpay:TmnCode"
        : string.IsNullOrWhiteSpace(HashSecret) ? "thiếu Vnpay:HashSecret"
        : string.IsNullOrWhiteSpace(ReturnUrl) ? "thiếu Vnpay:ReturnUrl"
        : null;
}
