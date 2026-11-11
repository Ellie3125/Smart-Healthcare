using Notifications.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.Holds;

/// <summary>
/// Giữ chỗ và hết hạn giữ chỗ (Giải pháp v2.14 §1.2, §5.1). Mỗi lệnh chạy trong một <see cref="IUnitOfWork"/>.
/// Giữ chỗ là lệnh đồng bộ: bản ghi idempotency, Operation và tác dụng commit cùng nhau (§4), nên gửi lặp hoặc gửi lại
/// sau khi mất phản hồi trả cùng OperationId và cùng kết quả; lỗi trước commit không để lại gì.
/// </summary>
public sealed class HoldService(IHoldStore store, IUnitOfWork uow, IIdempotencyStore idempotency, IOperationTracker operations)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(5);
    public const int ExpireBatchSize = 100;
    public const string OperationType = "Scheduling.HoldSlot";

    public Task<HoldOutcome> HoldAsync(HoldRequest request, CancellationToken ct) =>
        IsValid(request) && request.DoctorRankId != Guid.Empty
            ? uow.ExecuteAsync(t => HoldOnceAsync(request, null, t), ct)
            : Task.FromResult(new HoldOutcome(HoldResult.InvalidInput));

    public Task<HoldOutcome> HoldFromWebAsync(WebHoldRequest request,
        Func<CancellationToken, Task<Guid?>> resolveRankAsync, CancellationToken ct)
    {
        var hold = new HoldRequest(request.BookingAttemptId, request.PatientId, request.AppUserId,
            request.DoctorId, Guid.Empty, request.ServiceDate, BookingChannel.Web, request.IdempotencyKey);
        return IsValid(hold)
            ? uow.ExecuteAsync(t => HoldOnceAsync(hold, resolveRankAsync, t), ct)
            : Task.FromResult(new HoldOutcome(HoldResult.InvalidInput));
    }

    private async Task<HoldOutcome> HoldOnceAsync(HoldRequest r,
        Func<CancellationToken, Task<Guid?>>? resolveRankAsync, CancellationToken ct)
    {
        var scope = Idempotent.Scope(r.AppUserId);
        var day = r.ServiceDate.ToString("yyyy-MM-dd");
        var hash = resolveRankAsync is null
            ? Idempotent.Hash(r.BookingAttemptId, r.PatientId, r.DoctorId, r.DoctorRankId, day, r.Channel)
            : Idempotent.Hash(r.BookingAttemptId, r.PatientId, r.DoctorId, day, r.Channel);
        var operationId = Guid.CreateVersion7();
        switch (await idempotency.BeginAsync(scope, OperationType, r.IdempotencyKey, hash, Idempotent.Retention, operationId, ct))
        {
            case IdempotencyBeginResult.Replay replay:
                return Idempotent.Deserialize<HoldOutcome>(replay.BodyJson);
            case IdempotencyBeginResult.InProgress inProgress:
                return new HoldOutcome(HoldResult.InProgress, OperationId: inProgress.OperationId);
            case IdempotencyBeginResult.Conflict:
                return new HoldOutcome(HoldResult.IdempotencyConflict);
        }

        await operations.StartAsync(new StartOperation(operationId, r.AppUserId, Notifications.Contracts.OperationType.HoldSlot,
            TargetEntityType.BookingAttempt, r.BookingAttemptId, r.BookingAttemptId), ct);
        var request = r;
        if (resolveRankAsync is not null)
        {
            // Gửi lặp dùng kết quả cũ dù hạng bác sĩ đổi. Khóa Reservation phải duy nhất toàn bảng.
            var rankId = await resolveRankAsync(ct);
            request = r with { DoctorRankId = rankId ?? Guid.Empty, IdempotencyKey = operationId.ToString("N") };
        }

        var outcome = request.DoctorRankId == Guid.Empty
            ? new HoldOutcome(HoldResult.DoctorNotFound, OperationId: operationId)
            : await store.HoldAsync(request, HoldDuration, ct) with { OperationId = operationId };
        var held = outcome.Result == HoldResult.Held;
        await operations.CompleteAsync(operationId, held ? OperationStatus.Succeeded : OperationStatus.NeedsAction,
            held ? null : outcome.Result.ToString(), ct);
        await idempotency.CompleteAsync(scope, OperationType, r.IdempotencyKey, StatusCode(outcome.Result),
            Idempotent.Serialize(outcome), operationId, ct);
        return outcome;
    }

    /// <summary>Mã HTTP lưu cùng bản ghi idempotency để endpoint (sau này) phát lại đúng phản hồi.</summary>
    public static int StatusCode(HoldResult result) => result switch
    {
        HoldResult.Held => 201,
        HoldResult.InvalidInput => 400,
        HoldResult.AttemptNotFound or HoldResult.DoctorNotFound or HoldResult.DayNotFound => 404,
        _ => 409,
    };

    /// <summary>Job Hangfire định kỳ: hết hạn các giữ chỗ đã quá ExpiresAt, mỗi giữ chỗ một transaction. Trả số giữ chỗ đã hết hạn.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var expired = 0;
        foreach (var reservationId in await store.FindDueAsync(ExpireBatchSize, ct))
        {
            if (await uow.ExecuteAsync(t => store.ExpireAsync(reservationId, t), ct))
                expired++;
        }

        return expired;
    }

    private static bool IsValid(HoldRequest r) =>
        r.BookingAttemptId != Guid.Empty && r.PatientId != Guid.Empty && r.AppUserId != Guid.Empty
        && r.DoctorId != Guid.Empty && r.ServiceDate > DateOnly.MinValue && Enum.IsDefined(r.Channel)
        && Idempotent.IsValidKey(r.IdempotencyKey);
}

public enum HoldResult
{
    Held,
    InvalidInput,
    /// <summary>Không có BookingAttempt, hoặc thuộc bệnh nhân khác.</summary>
    AttemptNotFound,
    /// <summary>BookingAttempt không ở Draft/NeedsReselection (đang giữ chỗ, đã thanh toán, đã xác nhận...).</summary>
    InvalidAttemptState,
    DayNotFound,
    /// <summary>IsOpen = false hoặc SessionStatus khác Scheduled/InProgress.</summary>
    DayClosed,
    /// <summary>Giờ DB ≥ BookingCutoffTimeUtc.</summary>
    CutoffPassed,
    Full,
    /// <summary>Bệnh nhân đã có claim hoạt động với bác sĩ và ngày này.</summary>
    AlreadyClaimed,
    /// <summary>Cùng Idempotency-Key đang được xử lý.</summary>
    InProgress,
    /// <summary>Cùng Idempotency-Key nhưng khác nội dung.</summary>
    IdempotencyConflict,
    DoctorNotFound,
}

/// <param name="DoctorRankId">Hạng bác sĩ lấy từ danh mục phía server (không tin client); dùng tính phí khám áp dụng khi xác nhận (W2-06).</param>
public sealed record HoldRequest(Guid BookingAttemptId, Guid PatientId, Guid AppUserId, Guid DoctorId, Guid DoctorRankId,
    DateOnly ServiceDate, BookingChannel Channel, string IdempotencyKey);

public sealed record WebHoldRequest(Guid BookingAttemptId, Guid PatientId, Guid AppUserId, Guid DoctorId,
    DateOnly ServiceDate, string IdempotencyKey);

public sealed record HoldOutcome(HoldResult Result, Guid? ReservationId = null, DateTime? ExpiresAtUtc = null, Guid? OperationId = null);

/// <summary>Các method phải chạy bên trong <see cref="IUnitOfWork"/>, trừ <see cref="FindDueAsync"/>.</summary>
public interface IHoldStore
{
    Task<HoldOutcome> HoldAsync(HoldRequest request, TimeSpan holdDuration, CancellationToken ct);

    Task<IReadOnlyList<Guid>> FindDueAsync(int limit, CancellationToken ct);

    /// <summary>Trả true nếu lần gọi này chuyển giữ chỗ sang Expired; false nếu đã xác nhận, đã hết hạn hoặc chưa tới hạn.</summary>
    Task<bool> ExpireAsync(Guid reservationId, CancellationToken ct);
}
