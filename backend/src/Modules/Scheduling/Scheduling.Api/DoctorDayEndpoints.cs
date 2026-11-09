using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.DoctorDays;

namespace Scheduling.Api;

internal static class DoctorDayEndpoints
{
    public static void MapDoctorDayEndpoints(this RouteGroupBuilder scheduling)
    {
        var days = scheduling.MapGroup("/admin/doctor-days").RequireAuthorization("AdminOnly");

        days.MapPost("/{doctorId:guid}/{serviceDate}", OpenAsync)
            .WithSummary("Mở ngày trực bác sĩ (Staff Admin)")
            .Produces<DoctorDayResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        days.MapPost("/{doctorId:guid}/{serviceDate}/close", CloseAsync)
            .WithSummary("Đóng ngày trực bác sĩ (Staff Admin)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> OpenAsync(Guid doctorId, DateOnly serviceDate, DoctorDayService service, CancellationToken ct)
    {
        var (status, quota) = await service.OpenAsync(doctorId, serviceDate, ct);
        if (quota is null)
        {
            return Problem(status);
        }

        var response = new DoctorDayResponse(quota.Id, quota.DoctorId, quota.AssignedRoomId, quota.ServiceDate, quota.Capacity,
            quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount, quota.SessionStartTimeUtc, quota.BookingCutoffTimeUtc,
            quota.RescheduleCutoffTimeUtc, quota.CheckInDeadlineUtc!.Value, quota.SessionStatus.ToString(), quota.IsOpen);
        return TypedResults.Created((string?)null, response);
    }

    private static async Task<IResult> CloseAsync(Guid doctorId, DateOnly serviceDate, DoctorDayService service, CancellationToken ct)
    {
        var status = await service.CloseAsync(doctorId, serviceDate, ct);
        return status == DoctorDayResult.Closed ? TypedResults.NoContent() : Problem(status);
    }

    private static IResult Problem(DoctorDayResult status)
    {
        var (httpStatus, title) = status switch
        {
            DoctorDayResult.InvalidInput => (400, "Bác sĩ hoặc ngày phục vụ không hợp lệ."),
            DoctorDayResult.DoctorNotFound => (404, "Không tìm thấy bác sĩ có phòng được phân công."),
            DoctorDayResult.DayNotFound => (404, "Không tìm thấy ngày trực."),
            DoctorDayResult.AlreadyExists => (409, "Bác sĩ đã có phiên cho ngày này."),
            DoctorDayResult.HasBookings => (409, "Ngày trực đã có giữ chỗ hoặc lịch hẹn."),
            _ => throw new InvalidOperationException($"Unexpected doctor day result: {status}")
        };

        return TypedResults.Problem(statusCode: httpStatus, title: title, extensions: new Dictionary<string, object?>
        {
            ["code"] = status.ToString()
        });
    }

    private sealed record DoctorDayResponse(Guid Id, Guid DoctorId, Guid AssignedRoomId, DateOnly ServiceDate, int Capacity,
        int HeldCount, int AdmittedCount, int LockedCancelledCount, DateTime SessionStartTimeUtc, DateTime BookingCutoffTimeUtc,
        DateTime RescheduleCutoffTimeUtc, DateTime CheckInDeadlineUtc, string SessionStatus, bool IsOpen);
}
