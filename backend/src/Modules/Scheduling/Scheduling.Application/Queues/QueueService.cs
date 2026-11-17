using Clinical.Contracts;
using IdentityPatients.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.Queues;

/// <summary>
/// Lõi hàng đợi (W3-03, Giải pháp §6.1–6.3). Gọi người tiếp theo: chọn ứng viên theo luật trộn nhóm, rồi với từng ứng viên mở
/// một transaction ngắn, khóa bệnh nhân rồi ServiceStation (cùng thứ tự ở mọi lệnh nên không deadlock), kiểm lại trạng thái
/// trên DB và cấp phân bổ. Bệnh nhân đang ở điểm khác thì bỏ qua lượt này, vé giữ Waiting và số gốc.
/// InProgress mất kết nối chỉ cảnh báo phía UI: không có job tự giải phóng.
/// </summary>
public sealed class QueueService(IQueueStore store, IUnitOfWork uow, IClinicalModule clinical, IIdentityPatientsModule identity,
    IDbClock clock, QueuePolicy policy)
{
    public async Task<QueueOutcome> CallNextAsync(CallNext command, CancellationToken ct)
    {
        if (command.RoomId == Guid.Empty || command.PerformedByUserId == Guid.Empty || command.StationId == Guid.Empty)
            return new(QueueResult.InvalidInput);

        // Phòng phải tồn tại và đang hoạt động; tên phòng dùng cho station mặc định.
        if (await identity.GetRoomAsync(command.RoomId, ct) is not { } room)
            return new(QueueResult.StationNotFound);
        var station = await store.ResolveStationAsync(command.RoomId, command.StationId, room.Name, ct);
        if (station.StationId is not { } stationId)
            return new(station.Error);

        // Bấm gọi hai lần: station đã có người thì trả lại đúng vé đó, không cấp vé thứ hai.
        if (await store.GetAllocatedTicketAsync(stationId, ct) is { } current)
            return new(QueueResult.Called, current, AlreadyCalled: true);

        var now = await clock.GetUtcNowAsync(ct);
        var waiting = await store.ListWaitingAsync(command.RoomId, now, ct);
        var newStreak = await store.CountRecentNewCallsAsync(command.RoomId, now, policy.NewPerReturn, ct);
        foreach (var candidate in QueueFairness.Order(waiting, newStreak, now, policy))
        {
            var patientId = (await clinical.GetEncounterByIdAsync(candidate.EncounterId, ct))?.PatientId;
            if (patientId is null)
                continue;
            var attempt = await uow.ExecuteAsync(t => store.TryAllocateAsync(new Allocate(stationId, command.RoomId,
                candidate.TicketId, patientId.Value, candidate.EncounterId, command.PerformedByUserId), t), ct);
            switch (attempt.Result)
            {
                case QueueResult.Called:
                    return attempt;
                case QueueResult.StationBusy:
                    // Lệnh khác vừa cấp cho station này: trả vé đó như bấm hai lần.
                    return await store.GetAllocatedTicketAsync(stationId, ct) is { } taken
                        ? new(QueueResult.Called, taken, AlreadyCalled: true) : attempt;
            }
            // PatientBusy hoặc vé vừa bị trạm khác nhận: thử ứng viên kế tiếp.
        }

        return new(QueueResult.QueueEmpty);
    }

    public Task<QueueOutcome> StartAsync(TicketCommand command, CancellationToken ct) =>
        RunAsync(command, TicketTransition.Start, ct);

    public Task<QueueOutcome> FinishAsync(TicketCommand command, CancellationToken ct) =>
        RunAsync(command, TicketTransition.Finish, ct);

    public Task<QueueOutcome> RequeueAsync(TicketCommand command, CancellationToken ct) =>
        RunAsync(command, TicketTransition.Requeue, ct);

    private Task<QueueOutcome> RunAsync(TicketCommand command, TicketTransition transition, CancellationToken ct) =>
        command.TicketId == Guid.Empty || command.PerformedByUserId == Guid.Empty
            ? Task.FromResult(new QueueOutcome(QueueResult.InvalidInput))
            : uow.ExecuteAsync(t => store.TransitionAsync(command, transition, t), ct);
}

/// <summary>Luật trộn nhóm (Phạm vi: thí điểm 2 khám đầu rồi 1 quay lại, giữ thứ tự trong nhóm, có ngưỡng chờ chống bỏ đói).</summary>
public static class QueueFairness
{
    /// <param name="newStreak">Số lượt gần nhất liên tiếp của nhóm mới (khám đầu, CLS) trước lượt quay lại gần nhất.</param>
    public static IEnumerable<WaitingTicket> Order(IReadOnlyList<WaitingTicket> waiting, int newStreak, DateTime nowUtc,
        QueuePolicy policy)
    {
        // Nhóm mới theo số thứ tự cấp; nhóm quay lại theo thời điểm kích hoạt (IssuedAtUtc đặt lại lúc kích hoạt).
        var fresh = waiting.Where(x => x.TicketType != TicketType.ReturnConsultation)
            .OrderBy(x => x.SequenceNumber).ThenBy(x => x.IssuedAtUtc).ThenBy(x => x.TicketId).ToList();
        var back = waiting.Where(x => x.TicketType == TicketType.ReturnConsultation)
            .OrderBy(x => x.IssuedAtUtc).ThenBy(x => x.TicketId).ToList();
        if (fresh.Count == 0 || back.Count == 0)
            return fresh.Concat(back);

        var returnFirst = newStreak >= policy.NewPerReturn;
        var freshWait = nowUtc - fresh[0].IssuedAtUtc;
        var backWait = nowUtc - back[0].IssuedAtUtc;
        if (freshWait >= policy.MaxWait || backWait >= policy.MaxWait)
            returnFirst = backWait >= freshWait;
        return returnFirst ? back.Concat(fresh) : fresh.Concat(back);
    }
}

/// <param name="NewPerReturn">Số lượt nhóm mới trước một lượt quay lại khi cả hai nhóm có người (mặc định 2).</param>
/// <param name="MaxWait">Ngưỡng chờ: đầu nhóm chờ quá ngưỡng thì được ưu tiên (mặc định 20 phút).</param>
public sealed record QueuePolicy(int NewPerReturn, TimeSpan MaxWait)
{
    public static QueuePolicy Default { get; } = new(2, TimeSpan.FromMinutes(20));
}

/// <param name="StationId">Null: dùng station duy nhất của phòng, phòng chưa có station thì tạo station mặc định.</param>
public sealed record CallNext(Guid RoomId, Guid? StationId, Guid PerformedByUserId);

public sealed record TicketCommand(Guid TicketId, Guid PerformedByUserId, Guid? RequiredRoomId);

public enum TicketTransition { Start, Finish, Requeue }

public sealed record StationLookup(Guid? StationId, QueueResult Error = QueueResult.StationNotFound);

public sealed record WaitingTicket(Guid TicketId, Guid EncounterId, TicketType TicketType, int SequenceNumber, DateTime IssuedAtUtc);

public sealed record Allocate(Guid StationId, Guid RoomId, Guid TicketId, Guid PatientId, Guid EncounterId, Guid PerformedByUserId);

public sealed record CalledTicket(Guid QueueTicketId, string TicketNumber, int SequenceNumber, string TicketType, string QueueStatus,
    Guid TargetRoomId, Guid? StationId, Guid EncounterId, DateTime? CalledAtUtc);

public sealed record QueueOutcome(QueueResult Result, CalledTicket? Ticket = null, bool AlreadyCalled = false,
    Guid? StationId = null);

public enum QueueResult
{
    Called,
    InvalidInput,
    StationNotFound,
    /// <summary>Phòng có nhiều station đang hoạt động: phải chỉ định stationId.</summary>
    StationRequired,
    StationBusy,
    PatientBusy,
    TicketTaken,
    QueueEmpty,
    TicketNotFound,
    /// <summary>Vé thuộc phòng khác với phòng của tài khoản phòng (W3-08).</summary>
    WrongRoom,
    InvalidTicketState,
}

public interface IQueueStore
{
    /// <summary>Station đang hoạt động của phòng; phòng chưa có station nào thì tạo station mặc định (chốt 17/11/2026).</summary>
    Task<StationLookup> ResolveStationAsync(Guid roomId, Guid? stationId, string roomName, CancellationToken ct);
    Task<CalledTicket?> GetAllocatedTicketAsync(Guid stationId, CancellationToken ct);
    Task<IReadOnlyList<WaitingTicket>> ListWaitingAsync(Guid roomId, DateTime nowUtc, CancellationToken ct);
    Task<int> CountRecentNewCallsAsync(Guid roomId, DateTime nowUtc, int limit, CancellationToken ct);
    /// <summary>Trong transaction: khóa bệnh nhân rồi station, kiểm lại và cấp phân bổ.</summary>
    Task<QueueOutcome> TryAllocateAsync(Allocate command, CancellationToken ct);
    Task<QueueOutcome> TransitionAsync(TicketCommand command, TicketTransition transition, CancellationToken ct);

    /// <summary>Phòng bác sĩ được phân công hôm nay (giờ Việt Nam theo DB); null nếu không có phiên mở.</summary>
    Task<Guid?> GetDoctorRoomTodayAsync(Guid doctorId, CancellationToken ct);
}
