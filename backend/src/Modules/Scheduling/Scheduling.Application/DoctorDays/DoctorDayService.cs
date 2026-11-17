using IdentityPatients.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;

namespace Scheduling.Application.DoctorDays;

public sealed class DoctorDayService(IDoctorDirectory doctors, IDoctorDayStore store, DoctorDayPolicy policy)
{
    private static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    /// <param name="roomId">Phòng khám admin chọn cho phiên (Identity chưa có phòng theo ca trực của bác sĩ).</param>
    public async Task<(DoctorDayResult Status, DoctorDayQuota? Quota)> OpenAsync(
        Guid doctorId, DateOnly serviceDate, Guid roomId, CancellationToken ct)
    {
        if (!IsValid(doctorId, serviceDate) || roomId == Guid.Empty)
        {
            return (DoctorDayResult.InvalidInput, null);
        }

        if (!await doctors.IsActiveDoctorAsync(doctorId, ct))
        {
            return (DoctorDayResult.DoctorNotFound, null);
        }

        if (!await doctors.IsActiveClinicalRoomAsync(roomId, ct))
        {
            return (DoctorDayResult.RoomNotFound, null);
        }

        var newQuota = new DoctorDayQuota
        {
            DoctorId = doctorId,
            AssignedRoomId = roomId,
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
    Task<bool> IsActiveDoctorAsync(Guid doctorId, CancellationToken ct);

    /// <summary>Phòng tồn tại, đang hoạt động và là phòng khám (không phải phòng CLS).</summary>
    Task<bool> IsActiveClinicalRoomAsync(Guid roomId, CancellationToken ct);
}

/// <summary>Đọc bác sĩ và phòng từ module Identity.</summary>
public sealed class IdentityDoctorDirectory(IIdentityPatientsModule identity) : IDoctorDirectory
{
    public async Task<bool> IsActiveDoctorAsync(Guid doctorId, CancellationToken ct) =>
        (await identity.GetDoctorAsync(doctorId, ct))?.IsActive == true;

    public async Task<bool> IsActiveClinicalRoomAsync(Guid roomId, CancellationToken ct) =>
        await identity.GetRoomAsync(roomId, ct) is { IsActive: true, RoomType: "Clinical" };
}

public interface IDoctorDayStore
{
    Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct);
    Task<IReadOnlyList<DoctorDaySummary>> ListAsync(DateOnly serviceDate, CancellationToken ct);
    Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct);
}

public enum DoctorDayResult
{
    Opened,
    Closed,
    InvalidInput,
    DoctorNotFound,
    RoomNotFound,
    DayNotFound,
    AlreadyExists,
    HasBookings
}

/// <param name="OnlineBooked">Lịch hẹn còn hiệu lực đặt qua web/chatbot.</param>
/// <param name="CounterBooked">Lịch hẹn còn hiệu lực đặt tại quầy.</param>
public sealed record DoctorDaySummary(Guid Id, Guid DoctorId, Guid AssignedRoomId, DateOnly ServiceDate, int Capacity,
    int HeldCount, int AdmittedCount, int LockedCancelledCount, int OnlineBooked, int CounterBooked, bool IsOpen,
    SessionStatus SessionStatus);

public sealed record DoctorDayListItem(Guid Id, Guid DoctorId, string? DoctorName, string? DoctorRankName,
    string? SpecialtyName, Guid RoomId, string? RoomCode, string? RoomName, DateOnly ServiceDate, int TotalSlots,
    int HeldCount, int OnlineBookedCount, int WalkInBookedCount, int LockedCancelledCount, int RemainingSlots,
    string Status, string SessionStatus);

/// <summary>Danh sách ngày trực của một ngày cho admin, kèm tên bác sĩ/phòng đọc từ Identity.</summary>
public sealed class DoctorDayListQuery(IDoctorDayStore store, IIdentityPatientsModule identity)
{
    public async Task<IReadOnlyList<DoctorDayListItem>> ListAsync(DateOnly serviceDate, CancellationToken ct)
    {
        var days = await store.ListAsync(serviceDate, ct);
        if (days.Count == 0)
            return [];
        var doctors = (await identity.SearchDoctorsAsync(new IdentityPatients.Contracts.DoctorSearch(), ct)).ToDictionary(x => x.DoctorId);
        var specialties = (await identity.GetSpecialtiesAsync(ct)).ToDictionary(x => x.Id, x => x.Name);
        var rooms = (await identity.GetRoomsAsync(ct: ct)).ToDictionary(x => x.Id);
        return days.Select(d =>
        {
            var doctor = doctors.GetValueOrDefault(d.DoctorId);
            var room = rooms.GetValueOrDefault(d.AssignedRoomId);
            return new DoctorDayListItem(d.Id, d.DoctorId, doctor?.FullName, doctor?.RankName,
                doctor is null ? null : specialties.GetValueOrDefault(doctor.SpecialtyId), d.AssignedRoomId, room?.Code,
                room?.Name, d.ServiceDate, d.Capacity, d.HeldCount, d.OnlineBooked, d.CounterBooked, d.LockedCancelledCount,
                Math.Max(0, d.Capacity - d.HeldCount - d.AdmittedCount - d.LockedCancelledCount),
                d.IsOpen ? "Open" : "Closed", d.SessionStatus.ToString());
        }).ToList();
    }
}
