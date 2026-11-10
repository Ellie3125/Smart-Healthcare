using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Application.Holds;

/// <summary>Giữ chỗ và hết hạn giữ chỗ (Giải pháp v2.13 §1.2, §5.1). Mỗi lệnh chạy trong một <see cref="IUnitOfWork"/>.</summary>
public sealed class HoldService(IHoldStore store, IUnitOfWork uow)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(5);
    public const int ExpireBatchSize = 100;

    public Task<HoldOutcome> HoldAsync(HoldRequest request, CancellationToken ct) =>
        IsValid(request)
            ? uow.ExecuteAsync(t => store.HoldAsync(request, HoldDuration, t), ct)
            : Task.FromResult(new HoldOutcome(HoldResult.InvalidInput));

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
        && r.DoctorId != Guid.Empty && r.ServiceDate > DateOnly.MinValue && Enum.IsDefined(r.Channel);
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
}

public sealed record HoldRequest(Guid BookingAttemptId, Guid PatientId, Guid AppUserId, Guid DoctorId, DateOnly ServiceDate,
    BookingChannel Channel, string? IdempotencyKey = null);

public sealed record HoldOutcome(HoldResult Result, Guid? ReservationId = null, DateTime? ExpiresAtUtc = null);

/// <summary>Các method phải chạy bên trong <see cref="IUnitOfWork"/>, trừ <see cref="FindDueAsync"/>.</summary>
public interface IHoldStore
{
    Task<HoldOutcome> HoldAsync(HoldRequest request, TimeSpan holdDuration, CancellationToken ct);

    Task<IReadOnlyList<Guid>> FindDueAsync(int limit, CancellationToken ct);

    /// <summary>Trả true nếu lần gọi này chuyển giữ chỗ sang Expired; false nếu đã xác nhận, đã hết hạn hoặc chưa tới hạn.</summary>
    Task<bool> ExpireAsync(Guid reservationId, CancellationToken ct);
}
