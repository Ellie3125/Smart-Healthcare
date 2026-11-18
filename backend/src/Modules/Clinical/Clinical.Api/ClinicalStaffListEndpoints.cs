using System.Security.Claims;
using Clinical.Application.StaffLists;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Clinical.Api;

internal static class ClinicalStaffListEndpoints
{
    public static void MapStaffListRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/staff/nurse/queue", NurseAsync).RequireAuthorization(Policies.NurseOnly);
        group.MapGet("/staff/doctor/records", DoctorAsync).RequireAuthorization(Policies.DoctorOnly);
    }

    private static async Task<IResult> NurseAsync(Guid roomId, ClaimsPrincipal user,
        [FromServices] IIdentityPatientsModule identity, [FromServices] IDbClock clock,
        [FromServices] IClinicalStaffListReader reader, IServiceProvider services, CancellationToken ct, int page = 1, int pageSize = 50)
    {
        if (roomId == Guid.Empty || !ValidPage(page, pageSize)) return InvalidInput();
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
            return TypedResults.Forbid();
        var now = await clock.GetUtcNowAsync(ct);
        if (!await identity.IsNurseOnDutyAsync(actor, roomId, now, ct)) return TypedResults.Forbid();
        var tickets = await services.GetRequiredService<ISchedulingModule>().GetClinicalQueueAsync(roomId, now, page, pageSize, ct);
        var contexts = (await reader.NurseContextsAsync(tickets.Items.Select(t => t.EncounterId).Distinct().ToArray(), roomId, ct))
            .ToDictionary(e => e.EncounterId);
        var contacts = (await identity.GetPatientContactsAsync(contexts.Values.Select(e => e.PatientId).Distinct().ToArray(), ct))
            .ToDictionary(p => p.PatientId, p => p.FullName);
        var items = tickets.Items.Where(t => contexts.ContainsKey(t.EncounterId)).Select(t =>
        {
            var e = contexts[t.EncounterId];
            return new NurseQueueItem(e.EncounterId, t.QueueTicketId, t.TicketNumber, contacts.GetValueOrDefault(e.PatientId, ""),
                e.DoctorId, e.SpecialtyName, t.QueueStatus, e.EncounterStatus, e.Version, e.LastVitalsAtUtc);
        }).ToArray();
        return TypedResults.Ok(new StaffListPage<NurseQueueItem>(items, page, pageSize, tickets.HasMore));
    }

    private static async Task<IResult> DoctorAsync(ClaimsPrincipal user, [FromServices] IClinicalStaffListReader reader,
        [FromServices] IIdentityPatientsModule identity, CancellationToken ct, string? keyword = null, int page = 1, int pageSize = 50)
    {
        if (!ValidPage(page, pageSize)) return InvalidInput();
        Guid? encounterId = null;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            if (!Guid.TryParse(keyword, out var id) || id == Guid.Empty) return InvalidInput();
            encounterId = id;
        }
        if (!Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out var doctorId) || doctorId == Guid.Empty)
            return TypedResults.Forbid();
        var result = await reader.DoctorRecordsAsync(doctorId, encounterId, page, pageSize, ct);
        var contacts = (await identity.GetPatientContactsAsync(result.Items.Select(e => e.PatientId).Distinct().ToArray(), ct))
            .ToDictionary(p => p.PatientId, p => p.FullName);
        return TypedResults.Ok(result with { Items = result.Items.Select(e => e with { PatientName = contacts.GetValueOrDefault(e.PatientId, "") }).ToArray() });
    }

    private static bool ValidPage(int page, int size) => page is >= 1 and <= 1000 && size is >= 1 and <= 100;
    private static IResult InvalidInput() => TypedResults.Problem(statusCode: 400, title: "InvalidInput",
        extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
}
