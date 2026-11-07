namespace Billing.Application;

/// <summary>
/// Cổng thanh toán bên ngoài: mỗi cổng một cài đặt ở Infrastructure, hiện có VNPAY sandbox.
/// Cổng có thể gọi qua mạng, nên không bao giờ gọi bên trong transaction DB (MOD-05).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Tạo liên kết thanh toán cho mã tham chiếu. Gọi lại với cùng mã là an toàn: mã giao dịch của cổng chỉ ghi khi kết quả về.
    /// </summary>
    /// <exception cref="PaymentGatewayException">Cổng từ chối yêu cầu, hoặc chưa cấu hình cổng.</exception>
    Task<GatewayCheckout> CreateCheckoutAsync(GatewayCheckoutRequest request, CancellationToken ct);
}

/// <param name="Description">Nội dung thu hiển thị trên trang cổng, không dấu và không ký tự đặc biệt (VNPAY); không chứa thông tin bệnh án.</param>
/// <param name="CreatedAtUtc">Thời điểm tạo yêu cầu đã lưu (PaymentIntent.CreatedAtUtc). Dựng lại link hay gọi querydr đều ra cùng giá trị.</param>
/// <param name="CustomerIp">IP của người trả, nếu biết.</param>
public sealed record GatewayCheckoutRequest(
    string TransactionReference, decimal Amount, string Currency, string Description, DateTime CreatedAtUtc, DateTime ExpiresAtUtc, string? CustomerIp);

public sealed record GatewayCheckout(string PaymentUrl);

public sealed class PaymentGatewayException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Nhận kết quả cổng gửi về (callback/IPN, server tới server): xác minh chữ ký, người nhận, mã tham chiếu, số tiền rồi mới
/// ghi nhận (BR-002). Lỗi trả bằng <see cref="Billing.Contracts.BillingException"/>.
/// </summary>
public interface IPaymentCallbackHandler
{
    /// <param name="parameters">Toàn bộ tham số cổng gửi, đã giải mã URL.</param>
    Task<PaymentCallbackOutcome> HandleAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken ct);
}

public enum PaymentCallbackOutcome
{
    /// <summary>Thanh toán thành công vừa được ghi nhận; PaymentVerified đã vào Outbox.</summary>
    PaymentRecorded,

    /// <summary>Kết quả thành công đã ghi nhận từ trước (callback lặp); không đổi gì.</summary>
    PaymentAlreadyRecorded,

    /// <summary>Kết quả thất bại vừa được ghi nhận.</summary>
    FailureRecorded,

    /// <summary>Kết quả thất bại đã ghi nhận từ trước.</summary>
    FailureAlreadyRecorded,

    /// <summary>Kết quả thất bại đến sau khi giao dịch đã Paid; bỏ qua, không ghi đè.</summary>
    FailureIgnoredAlreadyPaid,
}
