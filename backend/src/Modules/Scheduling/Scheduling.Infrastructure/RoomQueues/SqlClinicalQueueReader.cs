using Microsoft.EntityFrameworkCore;
using Scheduling.Application.RoomQueues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.RoomQueues;

internal sealed class SqlClinicalQueueReader(SchedulingDbContext db) : IClinicalQueueReader
{
    public async Task<ClinicalQueuePage> ReadAsync(Guid roomId, DateTime atUtc, int page, int pageSize, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(atUtc.AddHours(7));
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var rows = await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.TargetRoomId == roomId && t.TicketType == TicketType.InitialConsultation
                && t.QueuedAtUtc >= start && t.QueuedAtUtc < start.AddDays(1)
                && (t.QueueStatus == QueueStatus.Waiting || t.QueueStatus == QueueStatus.Called
                    || t.QueueStatus == QueueStatus.InProgress || t.QueueStatus == QueueStatus.Deferred))
            .OrderBy(t => t.QueuedAtUtc).ThenBy(t => t.SequenceNumber).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(t => new ClinicalQueueTicket(t.Id, t.EncounterId, t.TicketNumber, t.QueueStatus.ToString(), t.QueuedAtUtc))
            .ToListAsync(ct);
        return new(rows.Count > pageSize, rows.Take(pageSize).ToArray());
    }
}
