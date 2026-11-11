using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.Appointments;

namespace Scheduling.Api;

internal static class PatientAppointmentEndpoints
{
    private const int MaxPage = 1000;
    private const int MaxPageSize = 50;

    public static void MapPatientAppointmentEndpoints(this RouteGroupBuilder scheduling)
    {
        var patient = scheduling.MapGroup("/patient/appointments").RequireAuthorization(Policies.PatientOnly);
        patient.MapGet("", ListAsync)
            .WithSummary("Lịch hẹn người gọi đã đặt, mới nhất trước (Patient)")
            .Produces<AppointmentPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        patient.MapGet("/{appointmentId:guid}", GetAsync)
            .WithSummary("Chi tiết lịch hẹn và mã lịch hẹn (Patient)")
            .Produces<AppointmentItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] IPatientAppointmentReader reader,
        [FromServices] IIdentityPatientsModule identity, CancellationToken ct, string page = "1", string pageSize = "20")
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        if (!int.TryParse(page, CultureInfo.InvariantCulture, out var pageNumber) || pageNumber is < 1 or > MaxPage
            || !int.TryParse(pageSize, CultureInfo.InvariantCulture, out var size) || size is < 1 or > MaxPageSize)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Phân trang không hợp lệ.",
                extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
        }

        var rows = await reader.ListAsync(userId, (pageNumber - 1) * size, size + 1, ct);
        var items = new List<AppointmentItem>(Math.Min(rows.Count, size));
        var access = new Dictionary<Guid, bool>();
        var doctors = new Dictionary<Guid, string?>();
        foreach (var row in rows.Take(size))
        {
            // Quyền Booking có thể đã bị thu hồi sau khi đặt: không trả lịch của hồ sơ đó.
            if (!access.TryGetValue(row.PatientId, out var allowed))
                access[row.PatientId] = allowed = await identity.CanAccessPatientAsync(userId, row.PatientId, AccessLevel.Booking, ct);
            if (allowed)
                items.Add(await ToItemAsync(row, identity, doctors, ct));
        }

        return TypedResults.Ok(new AppointmentPage(pageNumber, size, rows.Count > size, items));
    }

    private static async Task<IResult> GetAsync(Guid appointmentId, ClaimsPrincipal user,
        [FromServices] IPatientAppointmentReader reader, [FromServices] IIdentityPatientsModule identity, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var row = await reader.GetAsync(appointmentId, userId, ct);
        return row is not null && await identity.CanAccessPatientAsync(userId, row.PatientId, AccessLevel.Booking, ct)
            ? TypedResults.Ok(await ToItemAsync(row, identity, [], ct))
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy lịch hẹn.",
                extensions: new Dictionary<string, object?> { ["code"] = "AppointmentNotFound" });
    }

    private static async Task<AppointmentItem> ToItemAsync(PatientAppointment row, IIdentityPatientsModule identity,
        Dictionary<Guid, string?> doctors, CancellationToken ct)
    {
        if (!doctors.TryGetValue(row.DoctorId, out var doctorName))
            doctors[row.DoctorId] = doctorName = (await identity.GetDoctorAsync(row.DoctorId, ct))?.FullName;

        return new AppointmentItem(row.AppointmentId, row.AppointmentNumber, row.Status.ToString(), row.PatientId, row.DoctorId,
            doctorName, row.ServiceDate, row.AssignedRoomId, row.AppliedConsultationFee, "VND", row.ConfirmedAtUtc,
            row.CheckInDeadlineUtc, row.CancelledAtUtc);
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private sealed record AppointmentPage(int Page, int PageSize, bool HasMore, IReadOnlyList<AppointmentItem> Items);

    private sealed record AppointmentItem(Guid AppointmentId, string AppointmentNumber, string Status, Guid PatientId,
        Guid DoctorId, string? DoctorName, DateOnly ServiceDate, Guid AssignedRoomId, decimal AppliedConsultationFee,
        string Currency, DateTime ConfirmedAtUtc, DateTime? CheckInDeadlineUtc, DateTime? CancelledAtUtc);
}
