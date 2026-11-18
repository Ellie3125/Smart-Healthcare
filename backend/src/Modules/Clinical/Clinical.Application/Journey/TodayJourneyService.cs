using Billing.Contracts;
using IdentityPatients.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Clinical.Application.Journey;

public sealed record JourneyStep(string Id, string Title, string Location, string Status, string? QueueNumber, DateTime? UpdatedAt);
public sealed record TodayJourneyDto(Guid PatientProfileId, string PatientName, DateOnly ServiceDate, Guid? AppointmentId,
    string? EncounterStatus, string? CurrentCallText, int UnpaidClsCount, IReadOnlyList<JourneyStep> Steps);

public sealed class TodayJourneyService(IIdentityPatientsModule identity, IEncounterStore encounters, ISchedulingModule scheduling,
    IBillingModule billing, IDbClock clock)
{
    public async Task<TodayJourneyDto?> ReadAsync(Guid actor, Guid? patientId, CancellationToken ct)
    {
        var profile = await identity.GetJourneyPatientAsync(actor, patientId, ct);
        if (profile is null) return null;
        var now = await clock.GetUtcNowAsync(ct);
        var date = DateOnly.FromDateTime(now.AddHours(7));
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var encounter = await encounters.GetForPatientDayAsync(profile.PatientId, start, start.AddDays(1), ct);
        var snapshot = await scheduling.GetPatientJourneyAsync(profile.PatientId, encounter?.AppointmentId, encounter?.Id, now, ct);
        var rooms = snapshot.RoomId.HasValue || snapshot.Tickets.Count > 0
            ? (await identity.GetRoomsAsync(ct: ct)).ToDictionary(r => r.Id, r => r.Name) : [];
        var charges = encounter is null ? [] : (await billing.GetEncounterCostSummaryAsync(encounter.Id, ct)).Charges;
        var unpaid = charges.Count(c => c.ChargeType == ChargeType.CLSService && c.Status is ChargeStatus.Pending or ChargeStatus.PartiallyPaid
            && c.TotalAmount > c.PaidAmount);
        var names = charges.Where(c => c.ChargeType == ChargeType.CLSService && c.ServiceOrderItemId.HasValue)
            .GroupBy(c => c.ServiceOrderItemId!.Value).ToDictionary(g => g.Key, g => g.First().ItemNameSnapshot);
        var steps = snapshot.Tickets.Select(t => new JourneyStep(t.QueueTicketId.ToString(), t.TicketType switch
        {
            "InitialConsultation" => "Khám ban đầu", "ReturnConsultation" => "Quay lại bác sĩ",
            _ => t.ServiceOrderItemId is { } id && names.TryGetValue(id, out var name) ? name : "Cận lâm sàng"
        }, rooms.GetValueOrDefault(t.RoomId, ""), t.QueueStatus == "Finished" ? "Completed" : t.QueueStatus,
            t.TicketNumber, t.CompletedAtUtc ?? t.ServedAtUtc ?? t.CalledAtUtc ?? t.QueuedAtUtc)).ToList();
        if (encounter is null && snapshot.AppointmentId is { } appointment && snapshot.AppointmentStatus == "Scheduled")
            steps.Add(new("check-in-" + appointment, "Tiếp nhận tại quầy", snapshot.RoomId is { } room ? rooms.GetValueOrDefault(room, "") : "", "Waiting", null, null));
        var called = snapshot.Tickets.Where(t => t.QueueStatus == "Called").OrderByDescending(t => t.CalledAtUtc).ThenBy(t => t.QueueTicketId).FirstOrDefault();
        var callText = called is null ? null : "Đang gọi số " + called.TicketNumber
            + (rooms.TryGetValue(called.RoomId, out var location) ? " tại " + location : "");
        return new(profile.PatientId, profile.FullName, date, encounter?.AppointmentId ?? snapshot.AppointmentId,
            encounter?.Status.ToString(), callText, unpaid, steps);
    }
}
