using Microsoft.EntityFrameworkCore;
using Scheduling.Application.RoomQueues;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Scheduling.Infrastructure.RoomQueues;

internal sealed class SqlRoomQueueReader(SchedulingDbContext db, IDbClock clock) : IRoomQueueReader
{
    public async Task<RoomQueuePage?> GetStaffAsync(Guid roomId, Guid? doctorId, int page, int pageSize, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        if (doctorId is { } doctor && !await db.Set<DoctorDayQuota>().AsNoTracking()
                .AnyAsync(q => q.DoctorId == doctor && q.AssignedRoomId == roomId && q.ServiceDate == today, ct))
            return null;
        var rows = await TodayTickets(roomId, today)
            .Where(t => t.QueueStatus == QueueStatus.Waiting || t.QueueStatus == QueueStatus.Called
                || t.QueueStatus == QueueStatus.Deferred || t.QueueStatus == QueueStatus.InProgress)
            .OrderBy(t => t.QueuedAtUtc).ThenBy(t => t.SequenceNumber).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(t => new RoomQueueEntry(t.Id, t.TicketNumber, t.SequenceNumber, t.TicketType.ToString(),
                t.QueueStatus.ToString(), t.TargetStationId, t.CalledAtUtc, t.EncounterId, t.ServiceOrderItemId)).ToListAsync(ct);
        return new(today, page, pageSize, rows.Count > pageSize, rows.Take(pageSize).ToArray());
    }

    public async Task<IReadOnlyList<Guid>> GetTodayRoomIdsAsync(Guid? doctorId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        return await db.Set<DoctorDayQuota>().AsNoTracking()
            .Where(q => q.ServiceDate == today && (doctorId == null || q.DoctorId == doctorId))
            .Select(q => q.AssignedRoomId).Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RoomStation>> GetActiveStationsAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct) =>
        await db.Set<ServiceStation>().AsNoTracking()
            .Where(s => s.IsActive && roomIds.Contains(s.RoomId))
            .OrderBy(s => s.StationCode)
            .Select(s => new RoomStation(s.RoomId, new StaffStationOption(s.Id, s.StationCode, s.StationName)))
            .ToListAsync(ct);

    public async Task<PublicRoomDisplay> GetPublicAsync(Guid roomId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        var tickets = await TodayTickets(roomId, today)
            .Where(t => t.QueueStatus == QueueStatus.Called || t.QueueStatus == QueueStatus.InProgress)
            .OrderByDescending(t => t.CalledAtUtc ?? t.ServedAtUtc ?? t.IssuedAtUtc).ThenBy(t => t.Id)
            .Take(100).Select(t => new PublicQueueNumber(t.TicketNumber, t.SequenceNumber, null) { TicketType = t.TicketType.ToString() }).ToListAsync(ct);
        return new(tickets);
    }

    private IQueryable<QueueTicket> TodayTickets(Guid roomId, DateOnly today)
    {
        var start = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;
        return db.Set<QueueTicket>().AsNoTracking().Where(t => t.TargetRoomId == roomId
            && t.QueuedAtUtc >= start && t.QueuedAtUtc < start.AddDays(1));
    }
}
