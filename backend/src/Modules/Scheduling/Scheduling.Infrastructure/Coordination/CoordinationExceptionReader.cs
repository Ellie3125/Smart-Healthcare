using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Coordination;

public sealed class CoordinationExceptionReader(SchedulingDbContext db, IClinicalModule clinical, IIdentityPatientsModule identity)
    : ICoordinationExceptionReader
{
    public async Task<IReadOnlyList<CoordinationExceptionView>> ListAsync(int take, CancellationToken ct)
    {
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        var tickets = await db.Set<QueueTicket>().AsNoTracking().Where(q => q.QueueStatus != QueueStatus.Finished && q.QueueStatus != QueueStatus.Cancelled)
            .OrderBy(q => q.QueuedAtUtc).ThenBy(q => q.Id).ToListAsync(ct);
        var encounters = new Dictionary<Guid, EncounterDto?>(); var doctors = new Dictionary<Guid, DoctorDto?>(); var rooms = new Dictionary<Guid, RoomDto?>();
        var stationState = await db.Set<ServiceStation>().AsNoTracking().GroupBy(s => s.RoomId)
            .Select(g => new { Room = g.Key, Active = g.Any(s => s.IsActive) }).ToDictionaryAsync(x => x.Room, x => x.Active, ct);
        var cases = new List<CoordinationExceptionView>();
        // shortcut: public modules expose single-entity reads; request batch snapshots when active queues grow.
        foreach (var ticket in tickets)
        {
            if (!encounters.TryGetValue(ticket.EncounterId, out var encounter)) encounters[ticket.EncounterId] = encounter = await clinical.GetEncounterByIdAsync(ticket.EncounterId, ct);
            if (encounter is null || encounter.Status is "Completed" or "Abandoned") continue;
            if (!doctors.TryGetValue(encounter.OriginalDoctorId, out var doctor)) doctors[encounter.OriginalDoctorId] = doctor = await identity.GetDoctorAsync(encounter.OriginalDoctorId, ct);
            if (!rooms.TryGetValue(ticket.TargetRoomId, out var room)) rooms[ticket.TargetRoomId] = room = await identity.GetRoomAsync(ticket.TargetRoomId, ct);
            var reason = doctor is not { IsActive: true } ? "OriginalDoctorInactive"
                : room is not { IsActive: true } ? "TargetRoomInactive"
                : stationState.TryGetValue(ticket.TargetRoomId, out var active) && !active ? "NoActiveStation" : null;
            if (reason is null) continue;
            cases.Add(new(ticket.Id, ticket.EncounterId, ticket.TargetRoomId, encounter.OriginalDoctorId, ticket.QueueStatus.ToString(), reason,
                ticket.QueuedAtUtc, true));
            if (cases.Count == take) break;
        }
        return cases;
    }
}
