using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;

namespace Scheduling.Application.DoctorDays;

public sealed class DoctorDayService(IDoctorDirectory doctors, IDoctorDayStore store, DoctorDayPolicy policy)
{
    private static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    public async Task<(DoctorDayResult Status, DoctorDayQuota? Quota)> OpenAsync(
        Guid doctorId, DateOnly serviceDate, CancellationToken ct)
    {
        if (!IsValid(doctorId, serviceDate))
        {
            return (DoctorDayResult.InvalidInput, null);
        }

        var roomId = await doctors.GetAssignedRoomIdAsync(doctorId, ct);
        if (roomId is null || roomId == Guid.Empty)
        {
            return (DoctorDayResult.DoctorNotFound, null);
        }

        var newQuota = new DoctorDayQuota
        {
            DoctorId = doctorId,
            AssignedRoomId = roomId.Value,
            ServiceDate = serviceDate,
            Capacity = 30,
            SessionStartTimeUtc = ToUtc(serviceDate, new TimeOnly(6, 30)),
            BookingCutoffTimeUtc = ToUtc(serviceDate, new TimeOnly(9, 30)),
            RescheduleCutoffTimeUtc = ToUtc(serviceDate, new TimeOnly(5, 0)),
            CheckInDeadlineUtc = ToUtc(serviceDate, policy.CheckInDeadline),
            SessionStatus = SessionStatus.Scheduled,
            IsOpen = true
        };

        var quota = await store.TryOpenAsync(newQuota, ct);
        return quota is not null
            ? (DoctorDayResult.Opened, quota)
            : (DoctorDayResult.AlreadyExists, null);
    }

    public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
        IsValid(doctorId, serviceDate)
            ? store.CloseAsync(doctorId, serviceDate, ct)
            : Task.FromResult(DoctorDayResult.InvalidInput);

    private static bool IsValid(Guid doctorId, DateOnly serviceDate) =>
        doctorId != Guid.Empty && serviceDate > DateOnly.MinValue;

    private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), IctOffset).UtcDateTime;
}

public sealed record DoctorDayPolicy(TimeOnly CheckInDeadline);

public interface IDoctorDirectory
{
    Task<Guid?> GetAssignedRoomIdAsync(Guid doctorId, CancellationToken ct);
}

public interface IDoctorDayStore
{
    Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct);
    Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct);
}

public enum DoctorDayResult
{
    Opened,
    Closed,
    InvalidInput,
    DoctorNotFound,
    DayNotFound,
    AlreadyExists,
    HasBookings
}
