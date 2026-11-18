using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using IdentityPatients.Contracts;
using Workflow = Notifications.Contracts;

namespace Scheduling.Infrastructure.Queues;

internal sealed class SqlQueueStore(SchedulingDbContext db, IDbClock clock, AuditLogWriter audit, IIntegrationEventPublisher publisher,
    RoomAuditRequest? roomAudit = null, IIdentityPatientsModule? identity = null, Workflow.IOperationTracker? operations = null) : IQueueStore
{
    public async Task<StationLookup> ResolveStationAsync(Guid roomId, Guid? stationId, string roomName, CancellationToken ct)
    {
        var stations = await db.Set<ServiceStation>().AsNoTracking().Where(x => x.RoomId == roomId).ToListAsync(ct);
        if (stationId is { } id)
            return stations.Any(x => x.Id == id && x.IsActive) ? new(id) : new(null);
        var active = stations.Where(x => x.IsActive).ToList();
        if (active.Count > 1)
            return new(null, QueueResult.StationRequired);
        if (active.Count == 1)
            return new(active[0].Id);
        if (stations.Count > 0)
            return new(null);

        // Phòng chưa có station nào: tạo station mặc định; hai lệnh tạo cùng lúc thì bên thua đọc lại.
        var station = new ServiceStation
        {
            Id = Guid.CreateVersion7(), RoomId = roomId, StationCode = DefaultStationCode(roomId),
            StationName = roomName.Length > 200 ? roomName[..200] : roomName, IsActive = true,
        };
        db.Add(station);
        try
        {
            await db.SaveChangesAsync(ct);
            return new(station.Id);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(station).State = EntityState.Detached;
            var existing = await db.Set<ServiceStation>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.StationCode == station.StationCode && x.RoomId == roomId && x.IsActive, ct);
            return existing is null ? new(null) : new(existing.Id);
        }
    }

    public static string DefaultStationCode(Guid roomId) => $"DEFAULT-{roomId:N}";

    public Task<CalledTicket?> GetAllocatedTicketAsync(Guid stationId, CancellationToken ct) =>
        db.Set<ActiveStationAllocation>().AsNoTracking().Where(a => a.StationId == stationId)
            .Join(db.Set<QueueTicket>(), a => a.CurrentTicketId, t => t.Id, (a, t) => t)
            .Select(t => new CalledTicket(t.Id, t.TicketNumber, t.SequenceNumber, t.TicketType.ToString(),
                t.QueueStatus.ToString(), t.TargetRoomId, t.TargetStationId, t.EncounterId, t.CalledAtUtc))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<WaitingTicket>> ListWaitingAsync(Guid roomId, DateTime nowUtc, CancellationToken ct)
    {
        var start = StartOfVietnamDayUtc(nowUtc);
        return await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.TargetRoomId == roomId && t.QueuedAtUtc >= start && t.QueueStatus == QueueStatus.Waiting)
            .Select(t => new WaitingTicket(t.Id, t.EncounterId, t.TicketType, t.SequenceNumber, t.QueuedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<int> CountRecentNewCallsAsync(Guid roomId, DateTime nowUtc, int limit, CancellationToken ct)
    {
        var start = StartOfVietnamDayUtc(nowUtc);
        var recent = await db.Set<QueueTicket>().AsNoTracking()
            // Vé được gọi hôm nay đều vào hàng hôm nay (trừ vé quay lại kích hoạt hôm trước rồi gọi hôm nay: chỉ lệch bộ đếm trộn nhóm).
            .Where(t => t.TargetRoomId == roomId && t.QueuedAtUtc >= start && t.CalledAtUtc >= start)
            .OrderByDescending(t => t.CalledAtUtc).ThenByDescending(t => t.Id).Take(limit)
            .Select(t => t.TicketType).ToListAsync(ct);
        return recent.TakeWhile(x => x != TicketType.ReturnConsultation).Count();
    }

    public async Task<QueueOutcome> TryAllocateAsync(Allocate c, CancellationToken ct)
    {
        RequireTransaction();
        await LockAsync(PatientLock(c.PatientId), ct);
        await LockAsync(StationLock(c.StationId), ct);
        if (!await db.Set<ServiceStation>().FromSql($"SELECT * FROM scheduling.ServiceStation WITH (UPDLOCK,HOLDLOCK) WHERE Id={c.StationId}")
                .AsNoTracking().AnyAsync(s => s.RoomId == c.RoomId && s.IsActive, ct))
            return new(QueueResult.StationNotFound);
        if (await db.Set<ActiveStationAllocation>().AnyAsync(a => a.StationId == c.StationId, ct))
            return new(QueueResult.StationBusy);
        if (await db.Set<ActivePatientEngagement>().AnyAsync(e => e.PatientId == c.PatientId, ct))
            return new(QueueResult.PatientBusy);

        var now = await clock.GetUtcNowAsync(ct);
        var updated = await db.Set<QueueTicket>()
            .Where(t => t.Id == c.TicketId && t.TargetRoomId == c.RoomId && t.QueueStatus == QueueStatus.Waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.QueueStatus, QueueStatus.Called)
                .SetProperty(t => t.TargetStationId, c.StationId).SetProperty(t => t.CalledAtUtc, now), ct);
        if (updated == 0)
            return new(QueueResult.TicketTaken);

        db.Add(new ActivePatientEngagement
        {
            PatientId = c.PatientId, EncounterId = c.EncounterId, CurrentStationId = c.StationId, CurrentTicketId = c.TicketId,
            AcquiredAtUtc = now,
        });
        db.Add(new ActiveStationAllocation
        {
            StationId = c.StationId, CurrentPatientId = c.PatientId, CurrentTicketId = c.TicketId, AllocatedAtUtc = now,
        });
        await db.SaveChangesAsync(ct);
        await AuditAsync(c.TicketId, c.PerformedByUserId, QueueStatus.Waiting, QueueStatus.Called, now, ct);
        var called = await GetTicketAsync(c.TicketId, ct);
        await publisher.PublishAsync(new QueueCalled(Guid.CreateVersion7(), called.QueueTicketId, called.EncounterId, c.PatientId,
            c.RoomId, c.StationId, called.TicketNumber, called.SequenceNumber, called.TicketType, c.PerformedByUserId, now, roomAudit?.OperationId), c.TicketId, ct);
        return new(QueueResult.Called, called, StationId: c.StationId);
    }

    public async Task<QueueOutcome> TransitionAsync(TicketCommand c, TicketTransition transition, CancellationToken ct)
    {
        RequireTransaction();
        var ticket = await db.Set<QueueTicket>().AsNoTracking().SingleOrDefaultAsync(t => t.Id == c.TicketId, ct);
        if (ticket is null)
            return new(QueueResult.TicketNotFound);
        if (c.RequiredRoomId is { } room && room != ticket.TargetRoomId)
            return new(QueueResult.WrongRoom);

        var (from, to) = transition switch
        {
            TicketTransition.Start => (QueueStatus.Called, QueueStatus.InProgress),
            TicketTransition.Finish => (QueueStatus.InProgress, QueueStatus.Finished),
            _ => (QueueStatus.Called, QueueStatus.Waiting),
        };
        // Gửi lại lệnh đã có tác dụng: trả trạng thái hiện tại, không ghi lần hai.
        if (ticket.QueueStatus == to && (transition != TicketTransition.Requeue || ticket.CalledAtUtc is null))
            return new(QueueResult.Called, await GetTicketAsync(ticket.Id, ct), AlreadyCalled: true);
        if (ticket.QueueStatus != from)
            return new(QueueResult.InvalidTicketState, await GetTicketAsync(ticket.Id, ct));

        var allocation = await db.Set<ActiveStationAllocation>().AsNoTracking()
            .SingleOrDefaultAsync(a => a.CurrentTicketId == ticket.Id, ct);
        if (allocation is not null)
        {
            // Cùng thứ tự khóa với lệnh gọi: bệnh nhân rồi station.
            await LockAsync(PatientLock(allocation.CurrentPatientId), ct);
            await LockAsync(StationLock(allocation.StationId), ct);
        }

        var now = await clock.GetUtcNowAsync(ct);
        var query = db.Set<QueueTicket>().Where(t => t.Id == ticket.Id && t.QueueStatus == from);
        var updated = transition switch
        {
            TicketTransition.Start => await query.ExecuteUpdateAsync(s => s.SetProperty(t => t.QueueStatus, to)
                .SetProperty(t => t.ServedAtUtc, now), ct),
            TicketTransition.Finish => await query.ExecuteUpdateAsync(s => s.SetProperty(t => t.QueueStatus, to)
                .SetProperty(t => t.CompletedAtUtc, now), ct),
            _ => await query.ExecuteUpdateAsync(s => s.SetProperty(t => t.QueueStatus, to)
                .SetProperty(t => t.TargetStationId, (Guid?)null).SetProperty(t => t.CalledAtUtc, (DateTime?)null), ct),
        };
        if (updated == 0)
            return new(QueueResult.InvalidTicketState, await GetTicketAsync(ticket.Id, ct));

        // Khám xong hoặc trả về hàng chờ: giải phóng bệnh nhân và station trên nguồn có thẩm quyền (Giải pháp §6.3).
        if (transition != TicketTransition.Start)
        {
            await db.Set<ActivePatientEngagement>().Where(e => e.CurrentTicketId == ticket.Id).ExecuteDeleteAsync(ct);
            await db.Set<ActiveStationAllocation>().Where(a => a.CurrentTicketId == ticket.Id).ExecuteDeleteAsync(ct);
        }

        await AuditAsync(ticket.Id, c.PerformedByUserId, from, to, now, ct);
        return new(QueueResult.Called, await GetTicketAsync(ticket.Id, ct));
    }

    public async Task<Guid?> GetDoctorRoomTodayAsync(Guid doctorId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        return await db.Set<DoctorDayQuota>().AsNoTracking()
            .Where(q => q.DoctorId == doctorId && q.ServiceDate == today && q.IsOpen)
            .Select(q => (Guid?)q.AssignedRoomId).SingleOrDefaultAsync(ct);
    }

    private Task<CalledTicket> GetTicketAsync(Guid id, CancellationToken ct) => db.Set<QueueTicket>().AsNoTracking()
        .Where(t => t.Id == id)
        .Select(t => new CalledTicket(t.Id, t.TicketNumber, t.SequenceNumber, t.TicketType.ToString(), t.QueueStatus.ToString(),
            t.TargetRoomId, t.TargetStationId, t.EncounterId, t.CalledAtUtc))
        .SingleAsync(ct);

    private async Task AuditAsync(Guid ticketId, Guid userId, QueueStatus from, QueueStatus to, DateTime now, CancellationToken ct)
    {
        var entry = new AuditLog
        {
            Id = Guid.CreateVersion7(), PerformedByUserId = userId, TableName = "scheduling.QueueTicket", RecordId = ticketId,
            ActionType = ActionType.UPDATE, ChangedColumns = "QueueStatus",
            OldValuesJson = JsonSerializer.Serialize(new { QueueStatus = from.ToString() }),
            NewValuesJson = JsonSerializer.Serialize(new { QueueStatus = to.ToString() }), TimestampUtc = now,
        };
        if (roomAudit is { IsRoomAccount: true })
        {
            if (identity is null || operations is null) throw new InvalidOperationException("Room audit requires Identity and workflow participants.");
            var roomId = await db.Set<QueueTicket>().Where(x => x.Id == ticketId).Select(x => x.TargetRoomId).SingleAsync(ct);
            if (roomAudit.UserId != userId || roomAudit.RoomId != roomId) throw new RoomAuditDeniedException();
            var context = await identity.GetRoomOperationContextAsync(userId, roomId, roomAudit.SessionToken ?? "", now, ct)
                ?? throw new RoomAuditDeniedException();
            var operationId = Guid.CreateVersion7();
            await operations.StartAsync(new(operationId, userId, Workflow.OperationType.AllocateStation, Workflow.TargetEntityType.QueueTicket, ticketId, null), ct);
            roomAudit.OperationId = operationId;
            entry.WithRoomContext(new(context.AppUserId, context.RoomAccountId, context.RoomDeviceSessionId, context.RoomId,
                context.DeviceId, context.StaffShiftAssignmentId, context.ShiftLeaderStaffId, context.ShiftDate, context.ShiftName, operationId));
            entry.ClientIpAddress = roomAudit.ClientIpAddress;
            await audit.WriteAsync(entry, ct);
            await operations.CompleteAsync(operationId, Workflow.OperationStatus.Succeeded, null, ct);
        }
        else await audit.WriteAsync(entry, ct);
    }

    internal static string PatientLock(Guid id) => $"scheduling:patient:{id:N}";
    internal static string StationLock(Guid id) => $"scheduling:station:{id:N}";

    private Task LockAsync(string resource, CancellationToken ct) => db.AppLockAsync(resource, ct);

    internal static DateTime StartOfVietnamDayUtc(DateTime nowUtc) =>
        new DateTimeOffset(DateOnly.FromDateTime(nowUtc.AddHours(7)).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(7)).UtcDateTime;

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Lệnh hàng đợi phải chạy bên trong IUnitOfWork.");
    }
}

internal static class AppLock
{
    /// <summary>Khóa ứng dụng theo transaction hiện tại (sp_getapplock), tự nhả khi commit/rollback.</summary>
    public static async Task AppLockAsync(this DbContext db, string resource, CancellationToken ct)
    {
        var result = await db.Database.SqlQuery<int>($"""
            DECLARE @r int;
            EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            SELECT @r AS [Value];
            """).ToListAsync(ct);
        if (result.Single() < 0)
            throw new TimeoutException($"Không nhận được khóa {resource} (sp_getapplock = {result.Single()}).");
    }
}
