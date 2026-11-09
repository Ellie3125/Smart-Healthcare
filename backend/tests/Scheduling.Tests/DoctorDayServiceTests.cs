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
        var service = new DoctorDayService(new DoctorDirectory(RoomId), new Store(), new(new TimeOnly(hour, minute)));

        var (status, quota) = await service.OpenAsync(DoctorId, ServiceDate, Ct);

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
        var service = new DoctorDayService(new DoctorDirectory(null), new Store(), new(new TimeOnly(11, 0)));

        var (status, quota) = await service.OpenAsync(DoctorId, ServiceDate, Ct);

        Assert.Equal(DoctorDayResult.DoctorNotFound, status);
        Assert.Null(quota);
    }

    [Fact]
    public async Task OpenAndCloseRejectInvalidIdentityOrDate()
    {
        var service = new DoctorDayService(new DoctorDirectory(RoomId), new Store(), new(new TimeOnly(11, 0)));

        Assert.Equal(DoctorDayResult.InvalidInput, (await service.OpenAsync(Guid.Empty, ServiceDate, Ct)).Status);
        Assert.Equal(DoctorDayResult.InvalidInput, (await service.OpenAsync(DoctorId, DateOnly.MinValue, Ct)).Status);
        Assert.Equal(DoctorDayResult.InvalidInput, await service.CloseAsync(Guid.Empty, ServiceDate, Ct));
        Assert.Equal(DoctorDayResult.InvalidInput, await service.CloseAsync(DoctorId, DateOnly.MinValue, Ct));
    }

    private sealed class DoctorDirectory(Guid? roomId) : IDoctorDirectory
    {
        public Task<Guid?> GetAssignedRoomIdAsync(Guid doctorId, CancellationToken ct) => Task.FromResult(roomId);
    }

    private sealed class Store : IDoctorDayStore
    {
        public Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct) => Task.FromResult<DoctorDayQuota?>(quota);
        public Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
            throw new InvalidOperationException("Invalid requests must not reach the store.");
    }
}
