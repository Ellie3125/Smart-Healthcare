using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Scheduling.Infrastructure.Queues;

internal sealed class SqlClsTicketStore(SchedulingDbContext db, IDbClock clock) : IClsTicketStore
{
    public async Task<IReadOnlyList<Guid>> ListActiveRoomsAsync(Guid medicalServiceId, CancellationToken ct) =>
        await db.Set<MedicalServiceRoom>().AsNoTracking()
            .Where(r => r.MedicalServiceId == medicalServiceId && r.IsActive
                && db.Set<MedicalService>().Any(s => s.Id == medicalServiceId && s.IsActive))
            .OrderByDescending(r => r.IsDefaultRoom).ThenBy(r => r.RoomId)
            .Select(r => r.RoomId).ToListAsync(ct);

    public async Task<ClsTicketsCreated> CreateAsync(CreateClsTickets command, IReadOnlyDictionary<Guid, Guid> roomByService,
        CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tạo vé CLS phải chạy bên trong IUnitOfWork của Clinical.");

        var initial = await db.Set<QueueTicket>().AsNoTracking()
            .SingleOrDefaultAsync(t => t.EncounterId == command.EncounterId && t.TicketType == TicketType.InitialConsultation, ct)
            ?? throw new SchedulingException(SchedulingErrorCode.InitialTicketNotFound, "Lượt khám chưa có vé khám đầu.");

        // Một đợt chỉ định một lần tạo vé: khóa theo ServiceOrderId để lệnh gửi lại chờ rồi đọc vé đã có.
        await db.AppLockAsync($"scheduling:service-order:{command.ServiceOrderId:N}", ct);
        var itemIds = command.Lines.Select(l => (Guid?)l.ServiceOrderItemId).ToList();
        var existing = await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.ServiceOrderId == command.ServiceOrderId || itemIds.Contains(t.ServiceOrderItemId))
            .ToListAsync(ct);
        if (existing.Count > 0)
            return Replay(command, existing);

        var now = await clock.GetUtcNowAsync(ct);
        var day = DateOnly.FromDateTime(now.AddHours(7));
        var tickets = new List<QueueTicket>();
        foreach (var group in command.Lines.GroupBy(l => roomByService[l.MedicalServiceId]).OrderBy(g => g.Key))
        {
            // Số vé CLS theo phòng thực hiện và ngày (giờ Việt Nam), cấp dưới khóa của phòng.
            var prefix = $"{group.Key:N}-{day:yyyyMMdd}-";
            await db.AppLockAsync($"scheduling:ticket-number:{prefix}", ct);
            var next = await NextSequenceAsync(prefix, ct);
            foreach (var line in group)
                tickets.Add(NewTicket(command.EncounterId, group.Key, TicketType.CLS, QueueStatus.Waiting, prefix, next++, now,
                    serviceOrderItemId: line.ServiceOrderItemId));
        }

        // Vé quay lại ở phòng ban đầu, đánh số riêng trong phiên của vé khám đầu (tiền tố quota + R).
        var returnPrefix = initial.TicketNumber[..(initial.TicketNumber.LastIndexOf('-') + 1)] + "R";
        await db.AppLockAsync($"scheduling:ticket-number:{returnPrefix}", ct);
        var returnTicket = NewTicket(command.EncounterId, initial.TargetRoomId, TicketType.ReturnConsultation, QueueStatus.Deferred,
            returnPrefix, await NextSequenceAsync(returnPrefix, ct), now, serviceOrderId: command.ServiceOrderId);
        db.AddRange(tickets);
        db.Add(returnTicket);
        await db.SaveChangesAsync(ct);
        return new(tickets.Select(ToDto).ToList(), ToDto(returnTicket));
    }

    private static ClsTicketsCreated Replay(CreateClsTickets command, IReadOnlyList<QueueTicket> existing)
    {
        var returnTicket = existing.SingleOrDefault(t => t.TicketType == TicketType.ReturnConsultation
            && t.ServiceOrderId == command.ServiceOrderId);
        var cls = command.Lines.Select(l => existing.SingleOrDefault(t => t.ServiceOrderItemId == l.ServiceOrderItemId)).ToList();
        // Gửi lại phải đúng đợt và đúng các dòng đã tạo; khác thì là lỗi của bên gọi.
        if (returnTicket is null || cls.Any(t => t is null) || existing.Count != cls.Count + 1)
            throw new SchedulingException(SchedulingErrorCode.InvalidInput, "Đợt chỉ định đã có vé với nội dung khác.");
        return new(cls.Select(t => ToDto(t!)).ToList(), ToDto(returnTicket));
    }

    private async Task<int> NextSequenceAsync(string prefix, CancellationToken ct) =>
        (await db.Set<QueueTicket>().Where(t => t.TicketNumber.StartsWith(prefix))
            .MaxAsync(t => (int?)t.SequenceNumber, ct) ?? 0) + 1;

    private static QueueTicket NewTicket(Guid encounterId, Guid roomId, TicketType type, QueueStatus status, string prefix,
        int sequence, DateTime now, Guid? serviceOrderId = null, Guid? serviceOrderItemId = null) => new()
    {
        Id = Guid.CreateVersion7(), EncounterId = encounterId, TargetRoomId = roomId, TicketType = type, QueueStatus = status,
        SequenceNumber = sequence, TicketNumber = $"{prefix}{sequence:D3}", IssuedAtUtc = now,
        ServiceOrderId = serviceOrderId, ServiceOrderItemId = serviceOrderItemId,
    };

    private static QueueTicketDto ToDto(QueueTicket t) => new(t.Id, t.TicketType switch
    {
        TicketType.CLS => TicketKind.CLS,
        TicketType.ReturnConsultation => TicketKind.ReturnConsultation,
        _ => TicketKind.InitialConsultation,
    }, t.EncounterId, t.ServiceOrderItemId, t.TargetRoomId, t.TicketNumber, t.SequenceNumber, t.IssuedAtUtc);
}
