using System.Security.Claims;
using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Enums;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Billing.Api;

/// <summary>
/// Hoàn tiền (Phạm vi §5.6): admin duyệt, tiếp tân chi tiền mặt tại quầy, bệnh nhân xem yêu cầu hoàn của hồ sơ mình.
/// Yêu cầu hoàn do hệ thống tạo (hủy lịch được hoàn, PaidUnallocated), không có route để tự tạo.
/// </summary>
internal static class RefundEndpoints
{
    public static void MapRefundEndpoints(this RouteGroupBuilder billing)
    {
        var admin = billing.MapGroup("/admin/refunds").RequireAuthorization(Policies.AdminOnly);
        admin.MapGet("/", AdminListAsync)
            .WithSummary("Yêu cầu hoàn tiền, mới nhất trước; lọc theo status (Pending, Approved, Rejected, Settled) (Admin).")
            .Produces<IReadOnlyList<RefundView>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        admin.MapPost("/{refundRequestId:guid}/decision", DecideAsync)
            .WithSummary("Duyệt (Approve) hoặc từ chối (Reject) yêu cầu hoàn đang chờ; ghi AuditLog kèm ghi chú (Admin).")
            .Produces<RefundView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var cash = billing.MapGroup("/staff/cash-refunds").RequireAuthorization(Policies.ReceptionistOnly);
        cash.MapGet("/", CashListAsync)
            .WithSummary("Yêu cầu hoàn đã duyệt, chờ chi tiền mặt; lọc theo patientId nếu có (Receptionist).")
            .Produces<IReadOnlyList<RefundView>>();
        cash.MapPost("/{refundRequestId:guid}/disburse", DisburseAsync)
            .WithSummary("Tiếp tân xác nhận đã trả tiền mặt cho yêu cầu đã duyệt; trả số phiếu hoàn (Receptionist).")
            .Produces<CashRefundSlip>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        billing.MapGet("/patient/refunds", PatientListAsync)
            .RequireAuthorization(Policies.PatientOnly)
            .WithSummary("Yêu cầu hoàn tiền của một hồ sơ bệnh nhân mà tài khoản đang đại diện (Patient).")
            .Produces<IReadOnlyList<RefundView>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <param name="Decision">Approve hoặc Reject.</param>
    /// <param name="Note">Ghi chú lưu vào AuditLog, tối đa 500 ký tự; không ghi CCCD, số điện thoại.</param>
    internal sealed record RefundDecisionRequest(string? Decision, string? Note);

    private static async Task<IResult> AdminListAsync([FromQuery] string? status, HttpResponse response, [FromServices] RefundService refunds,
        CancellationToken ct)
    {
        RefundRequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RefundRequestStatus>(status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return Problem(StatusCodes.Status400BadRequest, "InvalidStatus", "Trạng thái không hợp lệ.");
            filter = parsed;
        }

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await refunds.ListAsync(new RefundQuery(Status: filter), ct));
    }

    private static async Task<IResult> DecideAsync(Guid refundRequestId, RefundDecisionRequest body, ClaimsPrincipal user, HttpContext http,
        [FromServices] RefundService refunds, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var adminUserId))
            return TypedResults.Unauthorized();
        bool approve;
        if (string.Equals(body.Decision, "Approve", StringComparison.OrdinalIgnoreCase))
            approve = true;
        else if (string.Equals(body.Decision, "Reject", StringComparison.OrdinalIgnoreCase))
            approve = false;
        else
            return Problem(StatusCodes.Status400BadRequest, "InvalidDecision", "Decision phải là Approve hoặc Reject.");
        if (body.Note?.Trim().Length > RefundService.NoteMaxLength)
            return Problem(StatusCodes.Status400BadRequest, "NoteTooLong", $"Ghi chú tối đa {RefundService.NoteMaxLength} ký tự.");

        var result = await refunds.DecideAsync(refundRequestId, adminUserId, approve, body.Note, http.Connection.RemoteIpAddress?.ToString(), ct);
        return result.Outcome switch
        {
            RefundChangeOutcome.Done => TypedResults.Ok(result.Refund),
            RefundChangeOutcome.InvalidState => Problem(StatusCodes.Status409Conflict, "RefundAlreadyDecided",
                $"Yêu cầu hoàn không còn chờ duyệt (đang {result.CurrentStatus})."),
            _ => Problem(StatusCodes.Status404NotFound, "RefundNotFound", "Không tìm thấy yêu cầu hoàn."),
        };
    }

    private static async Task<IResult> CashListAsync([FromQuery] Guid? patientId, HttpResponse response, [FromServices] RefundService refunds,
        CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await refunds.ListAsync(
            new RefundQuery(RefundRequestStatus.Approved, patientId is { } id && id != Guid.Empty ? id : null), ct));
    }

    private static async Task<IResult> DisburseAsync(Guid refundRequestId, ClaimsPrincipal user, HttpContext http,
        [FromServices] RefundService refunds, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var staffUserId))
            return TypedResults.Unauthorized();

        var (change, slip) = await refunds.DisburseCashAsync(refundRequestId, staffUserId, http.Connection.RemoteIpAddress?.ToString(), ct);
        return change.Outcome switch
        {
            RefundChangeOutcome.Done => TypedResults.Ok(slip),
            RefundChangeOutcome.InvalidState when change.CurrentStatus == nameof(RefundRequestStatus.Settled) =>
                Problem(StatusCodes.Status409Conflict, "RefundAlreadySettled", "Yêu cầu hoàn đã được chi."),
            RefundChangeOutcome.InvalidState => Problem(StatusCodes.Status409Conflict, "RefundNotApproved",
                $"Yêu cầu hoàn chưa được duyệt (đang {change.CurrentStatus})."),
            _ => Problem(StatusCodes.Status404NotFound, "RefundNotFound", "Không tìm thấy yêu cầu hoàn."),
        };
    }

    private static async Task<IResult> PatientListAsync([FromQuery] Guid? patientId, ClaimsPrincipal user, HttpResponse response,
        [FromServices] IIdentityPatientsModule identity, [FromServices] RefundService refunds, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        if (patientId is not { } id || id == Guid.Empty)
            return Problem(StatusCodes.Status400BadRequest, "PatientIdRequired", "Thiếu patientId của hồ sơ bệnh nhân.");
        // Không phân biệt "không có hồ sơ" và "không có quyền" để không lộ hồ sơ của người khác.
        if (!await identity.CanAccessPatientAsync(userId, id, AccessLevel.Booking, ct))
            return Problem(StatusCodes.Status404NotFound, "PatientNotFound", "Không tìm thấy hồ sơ bệnh nhân.");

        response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await refunds.ListAsync(new RefundQuery(PatientId: id), ct));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private static IResult Problem(int statusCode, string code, string title) =>
        TypedResults.Problem(statusCode: statusCode, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
