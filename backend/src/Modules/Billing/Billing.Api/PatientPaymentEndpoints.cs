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

        // Phí CLS trả theo từng đợt chỉ định (Phạm vi nghiệp vụ §5.6). Sau khi chuyển sang VNPAY, web hỏi trạng thái qua
        // GET /patient/payments/{paymentIntentId} như phí khám.
        var cls = billing.MapGroup("/patient/cls-batches").RequireAuthorization(Policies.PatientOnly);
        cls.MapGet("/", ListClsBatchesAsync)
            .WithSummary("Các đợt chỉ định CLS trong ngày của bệnh nhân và số tiền còn phải trả (Patient).")
            .Produces<IReadOnlyList<PatientClsBatch>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        cls.MapPost("/{serviceOrderId:guid}/vnpay-checkout", ClsCheckoutAsync)
            .WithSummary("Tạo yêu cầu thanh toán cho phần còn phải trả của đợt CLS (hạn 15 phút, không quá hết ngày khám) và trả liên kết VNPAY (Patient). Gọi lại được.")
            .Produces<CheckoutResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Phạm vi nghiệp vụ §5.5: năm chỉ tiêu chi phí của lượt khám, kèm từng khoản.
        billing.MapGet("/patient/encounters/{encounterId:guid}/cost-summary", CostSummaryAsync)
            .RequireAuthorization(Policies.PatientOnly)
            .WithSummary("Tổng hợp chi phí lượt khám: phải trả, đã trả, đã hoàn, thực thu, còn phải trả, tiền chờ hoàn (Patient).")
            .Produces<EncounterCostSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CostSummaryAsync(Guid encounterId, ClaimsPrincipal user, HttpResponse response,
        [FromServices] PatientCostSummaryService costs, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var summary = await costs.GetAsync(encounterId, userId, ct);
        if (summary is null)
            return Problem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy chi phí của lượt khám.");

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(summary);
    }

    private static async Task<IResult> ListClsBatchesAsync([FromQuery] Guid? patientId, ClaimsPrincipal user, HttpResponse response,
        [FromServices] PatientClsPaymentService cls, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        if (patientId is not { } id || id == Guid.Empty)
            return Problem(StatusCodes.Status400BadRequest, "PatientIdRequired", "Thiếu patientId của hồ sơ bệnh nhân.");

        var batches = await cls.ListTodayAsync(id, userId, ct);
        if (batches is null)
            return Problem(StatusCodes.Status404NotFound, "PatientNotFound", "Không tìm thấy hồ sơ bệnh nhân.");

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(batches);
    }

    private static async Task<IResult> ClsCheckoutAsync(Guid serviceOrderId, ClaimsPrincipal user, HttpContext http,
        [FromServices] PatientClsPaymentService cls, [FromServices] ILoggerFactory logs, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var result = await cls.StartCheckoutAsync(serviceOrderId, userId, http.Connection.RemoteIpAddress?.ToString(), ct);
            if (result.Checkout is not { } checkout)
            {
                return result.Outcome switch
                {
                    ClsPaymentOutcome.NothingToPay => Problem(StatusCodes.Status409Conflict, "ClsBatchNothingToPay",
                        "Đợt chỉ định đã được thanh toán đủ hoặc đã hủy."),
                    ClsPaymentOutcome.PastDeadline => Problem(StatusCodes.Status409Conflict, "ClsBatchExpired",
                        "Đã hết hạn thanh toán của đợt chỉ định (hết ngày khám)."),
                    ClsPaymentOutcome.OpenedByAnotherPayer => Problem(StatusCodes.Status409Conflict, "PaymentInProgress",
                        "Đợt chỉ định đang được thanh toán bởi tài khoản khác."),
                    _ => Problem(StatusCodes.Status404NotFound, "ClsBatchNotFound", "Không tìm thấy đợt chỉ định."),
                };
            }

            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CheckoutResponse(checkout.PaymentIntentId, checkout.PaymentUrl, checkout.Amount,
                checkout.Currency, checkout.ExpiresAtUtc));
        }
        catch (BillingException ex) when (ex.Code is BillingErrorCode.PaymentConflict or BillingErrorCode.ChargeNotPayable
                                              or BillingErrorCode.InvalidRequest or BillingErrorCode.GatewayUnavailable
                                              or BillingErrorCode.PaymentNotFound)
        {
            logs.CreateLogger("Billing.PatientPayments").LogWarning("Không tạo được thanh toán cho đợt CLS {ServiceOrderId}: {Code} {Message}",
                serviceOrderId, ex.Code, ex.Message);
            return ex.Code switch
            {
                BillingErrorCode.PaymentConflict => Problem(StatusCodes.Status409Conflict, "PaymentInProgress",
                    "Đang có yêu cầu thanh toán khác cho đợt này; vui lòng thử lại sau ít phút."),
                BillingErrorCode.ChargeNotPayable => Problem(StatusCodes.Status409Conflict, "ClsBatchChanged",
                    "Đợt chỉ định vừa thay đổi; vui lòng tải lại trang."),
                BillingErrorCode.GatewayUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, "GatewayUnavailable",
                    "Chưa tạo được liên kết thanh toán, vui lòng thử lại."),
                BillingErrorCode.InvalidRequest => Problem(StatusCodes.Status409Conflict, "OnlinePaymentNotAvailable",
                    "Đợt chỉ định này không thanh toán online được."),
                _ => Problem(StatusCodes.Status404NotFound, "ClsBatchNotFound", "Không tìm thấy đợt chỉ định."),
            };
        }
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
