using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorDayServiceTests
{
    private static readonly DateOnly ServiceDate = new(2026, 11, 10);
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(11, 0, 10, 4, 0)]
    [InlineData(12, 15, 10, 5, 15)]
    [InlineData(6, 15, 9, 23, 15)]
    public async Task OpenUsesIctPolicyOnTheServiceDate(int hour, int minute, int utcDay, int utcHour, int utcMinute)
    {
        var service = new DoctorDayService(new DoctorDirectory(true, true), new Store(), new(new TimeOnly(hour, minute)));

        var (status, quota) = await service.OpenAsync(DoctorId, ServiceDate, RoomId, Ct);

        Assert.Equal(DoctorDayResult.Opened, status);
        Assert.NotNull(quota);
        Assert.Equal(DoctorId, quota.DoctorId);
        Assert.Equal(RoomId, quota.AssignedRoomId);
        Assert.Equal(ServiceDate, quota.ServiceDate);
        Assert.Equal(30, quota.Capacity);
        Assert.Equal((0, 0, 0), (quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount));
        Assert.True(quota.IsOpen);
        Assert.Equal(SessionStatus.Scheduled, quota.SessionStatus);
        Assert.Equal(new DateTime(2026, 11, 9, 23, 30, 0, DateTimeKind.Utc), quota.SessionStartTimeUtc);
        Assert.Equal(new DateTime(2026, 11, 10, 2, 30, 0, DateTimeKind.Utc), quota.BookingCutoffTimeUtc);
        Assert.Equal(new DateTime(2026, 11, 9, 22, 0, 0, DateTimeKind.Utc), quota.RescheduleCutoffTimeUtc);
        Assert.Equal(new DateTime(2026, 11, utcDay, utcHour, utcMinute, 0, DateTimeKind.Utc), quota.CheckInDeadlineUtc);
        Assert.Equal(DateTimeKind.Utc, quota.CheckInDeadlineUtc!.Value.Kind);
    }

    [Fact]
    public async Task OpenRejectsUnknownDoctor()
    {
        var service = new DoctorDayService(new DoctorDirectory(false, true), new Store(), new(new TimeOnly(11, 0)));

        var (status, quota) = await service.OpenAsync(DoctorId, ServiceDate, RoomId, Ct);

        Assert.Equal(DoctorDayResult.DoctorNotFound, status);
        Assert.Null(quota);
    }

    [Fact]
    public async Task OpenRejectsInactiveOrNonClinicalRoom()
    {
        var service = new DoctorDayService(new DoctorDirectory(true, false), new Store(), new(new TimeOnly(11, 0)));

        var (status, quota) = await service.OpenAsync(DoctorId, ServiceDate, RoomId, Ct);

        Assert.Equal(DoctorDayResult.RoomNotFound, status);
        Assert.Null(quota);
    }

    [Fact]
    public async Task OpenAndCloseRejectInvalidIdentityOrDate()
    {
        var service = new DoctorDayService(new DoctorDirectory(true, true), new Store(), new(new TimeOnly(11, 0)));

        Assert.Equal(DoctorDayResult.InvalidInput, (await service.OpenAsync(Guid.Empty, ServiceDate, RoomId, Ct)).Status);
        Assert.Equal(DoctorDayResult.InvalidInput, (await service.OpenAsync(DoctorId, DateOnly.MinValue, RoomId, Ct)).Status);
        Assert.Equal(DoctorDayResult.InvalidInput, (await service.OpenAsync(DoctorId, ServiceDate, Guid.Empty, Ct)).Status);
        Assert.Equal(DoctorDayResult.InvalidInput, await service.CloseAsync(Guid.Empty, ServiceDate, Ct));
        Assert.Equal(DoctorDayResult.InvalidInput, await service.CloseAsync(DoctorId, DateOnly.MinValue, Ct));
    }

    [Fact]
    public async Task ListJoinsIdentityNamesAndSplitsOnlineAndCounterBookings()
    {
        var roomId = Guid.NewGuid();
        var specialtyId = Guid.NewGuid();
        var known = new DoctorDaySummary(Guid.NewGuid(), DoctorId, roomId, ServiceDate, 30, 2, 5, 1, 3, 2, true, SessionStatus.Scheduled);
        var unknown = known with { Id = Guid.NewGuid(), DoctorId = Guid.NewGuid(), AssignedRoomId = Guid.NewGuid(),
            HeldCount = 30, IsOpen = false };
        var identity = ListIdentity.Create(
            new IdentityPatients.Contracts.DoctorDto[] { new(DoctorId, "BS A", specialtyId, "NOI", Guid.NewGuid(), "Thạc sĩ", true) },
            new IdentityPatients.Contracts.SpecialtyDto[] { new(specialtyId, "NOI", "Khoa Nội") },
            new IdentityPatients.Contracts.RoomDto[] { new(roomId, "PK01", "Phòng khám 1", "Clinical", true) });

        var items = await new DoctorDayListQuery(new ListStore(known, unknown), identity).ListAsync(ServiceDate, Ct);

        Assert.Equal(new DoctorDayListItem(known.Id, DoctorId, "BS A", "Thạc sĩ", "Khoa Nội", roomId, "PK01", "Phòng khám 1",
            ServiceDate, 30, 2, 3, 2, 1, 22, "Open", "Scheduled"), items[0]);
        // Bác sĩ/phòng không còn trong Identity: vẫn trả phiên, tên để null; suất còn không âm.
        Assert.Equal((null, null, 0, "Closed"), (items[1].DoctorName, items[1].RoomCode, items[1].RemainingSlots, items[1].Status));
    }

    private sealed class ListStore(params DoctorDaySummary[] days) : IDoctorDayStore
    {
        public Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDaySummary>> ListAsync(DateOnly serviceDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DoctorDaySummary>>(days);
        public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) => throw new NotSupportedException();
    }

    public class ListIdentity : System.Reflection.DispatchProxy
    {
        private object[] _results = [];

        public static IdentityPatients.Contracts.IIdentityPatientsModule Create(params object[] results)
        {
            var proxy = System.Reflection.DispatchProxy.Create<IdentityPatients.Contracts.IIdentityPatientsModule, ListIdentity>();
            ((ListIdentity)(object)proxy)._results = results;
            return proxy;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "SearchDoctorsAsync" => Task.FromResult((IReadOnlyList<IdentityPatients.Contracts.DoctorDto>)_results[0]),
            "GetSpecialtiesAsync" => Task.FromResult((IReadOnlyList<IdentityPatients.Contracts.SpecialtyDto>)_results[1]),
            "GetRoomsAsync" => Task.FromResult((IReadOnlyList<IdentityPatients.Contracts.RoomDto>)_results[2]),
            _ => throw new NotSupportedException(method.Name),
        };
    }

    private sealed class DoctorDirectory(bool doctorActive, bool roomActive) : IDoctorDirectory
    {
        public Task<bool> IsActiveDoctorAsync(Guid doctorId, CancellationToken ct) => Task.FromResult(doctorActive);
        public Task<bool> IsActiveClinicalRoomAsync(Guid roomId, CancellationToken ct) => Task.FromResult(roomActive);
    }

    private sealed class Store : IDoctorDayStore
    {
        public Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct) => Task.FromResult<DoctorDayQuota?>(quota);
        public Task<IReadOnlyList<DoctorDaySummary>> ListAsync(DateOnly serviceDate, CancellationToken ct) => throw new NotSupportedException();
        public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
            throw new InvalidOperationException("Invalid requests must not reach the store.");
    }
}
