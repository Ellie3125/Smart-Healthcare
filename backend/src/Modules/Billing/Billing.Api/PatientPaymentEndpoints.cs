using System.Security.Claims;
using Billing.Application;
using Billing.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Billing.Api;

/// <summary>
/// API thanh toán của web bệnh nhân (Giải pháp kỹ thuật v2.13 mục 5.4, bước 2 và 3). Web lấy liên kết VNPAY rồi chuyển
/// trình duyệt sang đó; sau khi VNPAY chuyển về Return URL, web hỏi trạng thái ở đây cho tới khi có kết quả đã xác minh.
/// </summary>
internal static class PatientPaymentEndpoints
{
    public static void MapPatientPaymentEndpoints(this RouteGroupBuilder billing)
    {
        var patient = billing.MapGroup("/patient/payments").RequireAuthorization(Policies.PatientOnly);
        patient.MapGet("/{paymentIntentId:guid}", GetAsync)
            .WithSummary("Trạng thái yêu cầu thanh toán (Patient). Chỉ đổi khi Billing đã xác minh IPN hoặc querydr.")
            .Produces<PatientPaymentStatus>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapPost("/{paymentIntentId:guid}/checkout", CheckoutAsync)
            .WithSummary("Tạo liên kết thanh toán VNPAY (Patient). Không ghi DB, gọi lại được.")
            .Produces<CheckoutResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetAsync(Guid paymentIntentId, ClaimsPrincipal user, HttpResponse response,
        [FromServices] PatientPaymentService payments, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var status = await payments.GetAsync(paymentIntentId, userId, ct);
        if (status is null)
            return PaymentNotFound();

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(status);
    }

    private static async Task<IResult> CheckoutAsync(Guid paymentIntentId, ClaimsPrincipal user, HttpContext http,
        [FromServices] PatientPaymentService payments, [FromServices] ILoggerFactory logs, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var checkout = await payments.StartCheckoutAsync(paymentIntentId, userId, http.Connection.RemoteIpAddress?.ToString(), ct);
            if (checkout is null)
                return PaymentNotFound();

            // Liên kết mang chữ ký của giao dịch; không để proxy hay trình duyệt lưu lại.
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CheckoutResponse(checkout.PaymentIntentId, checkout.PaymentUrl, checkout.Amount,
                checkout.Currency, checkout.ExpiresAtUtc));
        }
        catch (BillingException ex) when (ex.Code is BillingErrorCode.PaymentConflict or BillingErrorCode.InvalidRequest
                                              or BillingErrorCode.GatewayUnavailable or BillingErrorCode.PaymentNotFound)
        {
            // Chi tiết (cấu hình thiếu, trạng thái) chỉ ghi log, không trả cho người dùng.
            logs.CreateLogger("Billing.PatientPayments").LogWarning("Không tạo được liên kết thanh toán {PaymentIntentId}: {Code} {Message}",
                paymentIntentId, ex.Code, ex.Message);
            return ex.Code switch
            {
                BillingErrorCode.PaymentConflict => Problem(StatusCodes.Status409Conflict, "PaymentNotOpen",
                    "Yêu cầu thanh toán đã có kết quả hoặc đã quá hạn."),
                BillingErrorCode.InvalidRequest => Problem(StatusCodes.Status409Conflict, "OnlinePaymentNotAvailable",
                    "Yêu cầu này không thanh toán online được."),
                BillingErrorCode.GatewayUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, "GatewayUnavailable",
                    "Chưa tạo được liên kết thanh toán, vui lòng thử lại."),
                _ => PaymentNotFound(),
            };
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private static IResult PaymentNotFound() =>
        Problem(StatusCodes.Status404NotFound, "PaymentNotFound", "Không tìm thấy yêu cầu thanh toán.");

    private static IResult Problem(int statusCode, string code, string title) =>
        TypedResults.Problem(statusCode: statusCode, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    internal sealed record CheckoutResponse(Guid PaymentIntentId, string PaymentUrl, decimal Amount, string Currency, DateTime ExpiresAtUtc);
}
