using Billing.Application;
using System.Text.Json.Serialization;
using Billing.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Billing.Api;

public static class DependencyInjection
{
    public const string Tag = "Billing";

    /// <summary>Đường dẫn khai làm IPN URL trong trang quản trị merchant của VNPAY.</summary>
    public const string VnpayIpnPath = "/api/billing/payments/vnpay/ipn";

    public static IServiceCollection AddBillingApi(this IServiceCollection services) => services;

    public static RouteGroupBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/billing").WithTags(Tag);

        // Server tới server từ VNPAY: không có phiên người dùng; chỉ tin chữ ký HMAC-SHA512 trên tham số (BR-002).
        group.MapGet("/payments/vnpay/ipn", HandleVnpayIpnAsync)
            .AllowAnonymous()
            .WithSummary("Nhận kết quả thanh toán từ VNPAY (IPN). Luôn trả HTTP 200 kèm RspCode theo tài liệu VNPAY.")
            .Produces<VnpayIpnResponse>();

        return group;
    }

    /// <summary>Đúng tên trường VNPAY yêu cầu. RspCode 00 và 02 kết thúc; mã khác hoặc quá thời gian thì VNPAY gửi lại, tối đa 10 lần, cách 5 phút.</summary>
    public sealed record VnpayIpnResponse(
        [property: JsonPropertyName("RspCode")] string RspCode,
        [property: JsonPropertyName("Message")] string Message);

    private static readonly VnpayIpnResponse ConfirmSuccess = new("00", "Confirm Success");
    private static readonly VnpayIpnResponse AlreadyConfirmed = new("02", "Order already confirmed");
    private static readonly VnpayIpnResponse UnknownError = new("99", "Unknown error");

    private static async Task<IResult> HandleVnpayIpnAsync(HttpRequest request, IPaymentCallbackHandler handler, ILoggerFactory logs, CancellationToken ct)
    {
        var log = logs.CreateLogger("Billing.VnpayIpn");
        var parameters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        try
        {
            var outcome = await handler.HandleAsync(parameters, ct);
            return Results.Ok(outcome is PaymentCallbackOutcome.PaymentRecorded or PaymentCallbackOutcome.FailureRecorded ? ConfirmSuccess : AlreadyConfirmed);
        }
        catch (BillingException ex)
        {
            log.LogWarning("IPN VNPAY bị từ chối: {Code} {Message}", ex.Code, ex.Message);
            return Results.Ok(ResponseOf(ex.Code));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "IPN VNPAY lỗi khi xử lý.");
            return Results.Ok(UnknownError);
        }
    }

    // PaymentConflict trả 02 để VNPAY dừng gửi lại: gửi lại không đổi được kết quả, giao dịch cần đối soát tay (đã ghi log).
    private static VnpayIpnResponse ResponseOf(BillingErrorCode code) => code switch
    {
        BillingErrorCode.InvalidSignature => new("97", "Invalid signature"),
        BillingErrorCode.PaymentNotFound => new("01", "Order not found"),
        BillingErrorCode.AmountMismatch => new("04", "invalid amount"),
        BillingErrorCode.PaymentConflict => AlreadyConfirmed,
        _ => UnknownError,
    };
}
