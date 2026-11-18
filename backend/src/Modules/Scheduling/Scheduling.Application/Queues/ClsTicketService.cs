using IdentityPatients.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Scheduling.Application.Queues;

/// <summary>
/// Vé CLS + vé quay lại cho một đợt chỉ định (W3-04, docs/api/scheduling.md §2.3). Chạy trong unit of work của Clinical
/// (gọi lồng thì tham gia transaction bên ngoài): Clinical lỗi thì vé cũng rollback. Gọi lại cùng ServiceOrderId trả vé đã có.
/// Vé quay lại tạo ở Deferred (tạm hoãn tới khi đủ kết quả); W3-06 kích hoạt: Deferred → Waiting và ghi ActivatedAtUtc,
/// IssuedAtUtc giữ giờ phát (CLA-PERF-001, 17/11/2026).
/// </summary>
public sealed class ClsTicketService(IClsTicketStore store, IUnitOfWork uow, IIdentityPatientsModule identity)
{
    public async Task<ClsTicketsCreated> CreateAsync(CreateClsTickets command, CancellationToken ct)
    {
        if (command is null || command.EncounterId == Guid.Empty || command.ServiceOrderId == Guid.Empty
            || command.Lines is not { Count: > 0 }
            || command.Lines.Any(x => x.ServiceOrderItemId == Guid.Empty || x.MedicalServiceId == Guid.Empty)
            || command.Lines.Select(x => x.ServiceOrderItemId).Distinct().Count() != command.Lines.Count)
            throw new SchedulingException(SchedulingErrorCode.InvalidInput, "Đợt chỉ định không hợp lệ.");

        // Chọn phòng trước khi vào transaction để không giữ khóa trong lúc gọi Identity.
        var rooms = new Dictionary<Guid, Guid>();
        foreach (var serviceId in command.Lines.Select(x => x.MedicalServiceId).Distinct())
            rooms[serviceId] = await PickRoomAsync(serviceId, ct)
                ?? throw new SchedulingException(SchedulingErrorCode.MedicalServiceUnavailable,
                    "Dịch vụ không tồn tại, ngừng hoạt động hoặc không còn phòng thực hiện.");

        var result = await uow.ExecuteAsync(t => store.CreateAsync(command, rooms, t), ct);
        var roomNumbers = (await identity.GetRoomsAsync(ct: ct)).ToDictionary(r => r.Id, r => r.Code);
        QueueTicketDto Display(QueueTicketDto ticket) => ticket with
        {
            DisplayNumber = TicketDisplayNumber.Format(roomNumbers.GetValueOrDefault(ticket.TargetRoomId), ticket.Kind.ToString(), ticket.SequenceNumber)
        };
        return new(result.ClsTickets.Select(Display).ToArray(), Display(result.ReturnTicket));
    }

    private async Task<Guid?> PickRoomAsync(Guid medicalServiceId, CancellationToken ct)
    {
        foreach (var roomId in await store.ListActiveRoomsAsync(medicalServiceId, ct))
        {
            if (await identity.GetRoomAsync(roomId, ct) is not null)
                return roomId;
        }

        return null;
    }
}

public interface IClsTicketStore
{
    /// <summary>Phòng thực hiện đang bật của dịch vụ đang hoạt động, phòng mặc định trước.</summary>
    Task<IReadOnlyList<Guid>> ListActiveRoomsAsync(Guid medicalServiceId, CancellationToken ct);

    /// <summary>Trong transaction: kiểm vé khám đầu, cấp số dưới khóa, idempotent theo ServiceOrderId/ServiceOrderItemId.</summary>
    Task<ClsTicketsCreated> CreateAsync(CreateClsTickets command, IReadOnlyDictionary<Guid, Guid> roomByService, CancellationToken ct);
}
