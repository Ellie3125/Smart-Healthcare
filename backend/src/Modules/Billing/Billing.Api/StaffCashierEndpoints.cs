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
/// Quầy thu tiền mặt của tiếp tân (Giải pháp kỹ thuật v2.13 mục 5.5). Chỉ tài khoản tiếp tân. Số tiền do Billing tính,
/// tiếp tân chỉ xác nhận đã nhận tiền. Hiện thu phí CLS theo đợt chỉ định; phí khám tại quầy chờ luồng đặt lịch tại quầy.
/// </summary>
internal static class StaffCashierEndpoints
{
    public static void MapStaffCashierEndpoints(this RouteGroupBuilder billing)
    {
        var cashier = billing.MapGroup("/staff/cashier").RequireAuthorization(Policies.ReceptionistOnly);

        cashier.MapGet("/items", ListAsync)
            .WithSummary("Đợt CLS trong ngày cần thu (còn phải thu trước). Lọc theo patientId nếu có (Receptionist).")
            .Produces<IReadOnlyList<CashierClsItem>>();
        cashier.MapPost("/cls-batches/{serviceOrderId:guid}/collect-cash", CollectAsync)
            .WithSummary("Tiếp tân xác nhận đã nhận tiền mặt cho phần còn phải trả của đợt CLS; trả số phiếu thu (Receptionist).")
            .Produces<CashReceipt>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        cashier.MapGet("/shift-summary", SummaryAsync)
            .WithSummary("Tổng tiền mặt tiếp tân đã thu hôm nay (giờ Việt Nam) và danh sách phiếu thu để đối chiếu (Receptionist).")
            .Produces<CashierShiftSummary>();

        // Bệnh nhân mang bill đến quầy khi giao dịch online không được ghi nhận (Giải pháp kỹ thuật v2.13 mục 5.5).
        billing.MapPost("/staff/payments/querydr", QueryDrAsync)
            .RequireAuthorization(Policies.ReceptionistOrAdmin)
            .WithSummary("Tra trạng thái giao dịch ở VNPAY theo mã giao dịch (vnp_TxnRef); cổng báo đã thu thì ghi nhận như IPN (Receptionist, Admin).")
            .Produces<PaymentReconciliation>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        billing.MapGet("/admin/payments/reconcile", NeedingReviewAsync)
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Yêu cầu thanh toán online cần người xử lý (Unknown): cổng chưa có kết quả quá lâu, giao dịch đảo, lệch số tiền (Admin).")
            .Produces<IReadOnlyList<PaymentNeedingReview>>();
    }

    /// <param name="TransactionReference">Mã giao dịch in trên bill VNPAY (vnp_TxnRef).</param>
    internal sealed record QueryDrRequest(string? TransactionReference);

    private static async Task<IResult> QueryDrAsync(QueryDrRequest body, [FromServices] PaymentReconciliationService reconciliation,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TransactionReference) || body.TransactionReference.Trim().Length > 64)
            return Problem(StatusCodes.Status400BadRequest, "TransactionReferenceRequired", "Thiếu hoặc sai mã giao dịch.");

        var result = await reconciliation.ReconcileAsync(body.TransactionReference, ct);
        return result.Outcome == ReconcileOutcome.NotFound
            ? Problem(StatusCodes.Status404NotFound, "PaymentNotFound", "Không có giao dịch với mã này trong hệ thống.")
            : TypedResults.Ok(result);
    }

    private static async Task<IResult> NeedingReviewAsync(HttpResponse response, [FromServices] PaymentReconciliationService reconciliation,
        CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await reconciliation.ListNeedingReviewAsync(ct));
    }

    private static async Task<IResult> ListAsync([FromQuery] Guid? patientId, HttpResponse response,
        [FromServices] CashierService cashier, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await cashier.ListTodayAsync(patientId is { } id && id != Guid.Empty ? id : null, ct));
    }

    private static async Task<IResult> CollectAsync(Guid serviceOrderId, ClaimsPrincipal user, HttpContext http,
        [FromServices] CashierService cashier, [FromServices] ILoggerFactory logs, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var staffUserId))
            return TypedResults.Unauthorized();

        try
        {
            var result = await cashier.CollectClsAsync(serviceOrderId, staffUserId, http.Connection.RemoteIpAddress?.ToString(), ct);
            return result.Outcome switch
            {
                CashCollectionOutcome.Collected => TypedResults.Ok(result.Receipt),
                CashCollectionOutcome.NothingToPay => Problem(StatusCodes.Status409Conflict, "ClsBatchNothingToPay",
                    "Đợt chỉ định đã được thanh toán đủ hoặc đã hủy."),
                CashCollectionOutcome.PastDeadline => Problem(StatusCodes.Status409Conflict, "ClsBatchExpired",
                    "Đã hết hạn thanh toán của đợt chỉ định (hết ngày khám)."),
                _ => Problem(StatusCodes.Status404NotFound, "ClsBatchNotFound", "Không tìm thấy đợt chỉ định."),
            };
        }
        catch (BillingException ex) when (ex.Code is BillingErrorCode.ChargeNotPayable)
        {
            logs.CreateLogger("Billing.Cashier").LogWarning("Không thu được tiền mặt cho đợt CLS {ServiceOrderId}: {Message}",
                serviceOrderId, ex.Message);
            return Problem(StatusCodes.Status409Conflict, "ClsBatchChanged", "Đợt chỉ định vừa thay đổi; tải lại trước khi thu.");
        }
    }

    private static async Task<IResult> SummaryAsync(ClaimsPrincipal user, HttpResponse response,
        [FromServices] CashierService cashier, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var staffUserId))
            return TypedResults.Unauthorized();

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await cashier.GetTodaySummaryAsync(staffUserId, ct));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private static IResult Problem(int statusCode, string code, string title) =>
        TypedResults.Problem(statusCode: statusCode, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
