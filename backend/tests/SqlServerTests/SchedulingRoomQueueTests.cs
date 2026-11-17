using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.RoomQueues;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Xunit;

namespace SqlServerTests;

public sealed class SchedulingRoomQueueTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime Now = new(2026, 11, 16, 17, 30, 0, DateTimeKind.Utc); // 00:30 VN ngày 17.

    [Fact]
    public async Task Room_queue_filters_room_day_and_state_and_public_selects_only_called_numbers()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var services = ReaderServices(database);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IRoomQueueReader>();
        var room = Guid.NewGuid();
        await TicketAsync(database, room, "Waiting", 1, Now.AddMinutes(-5));
        await TicketAsync(database, room, "Called", 2, Now.AddMinutes(-4));
        await TicketAsync(database, room, "InProgress", 3, Now.AddMinutes(-3));
        await TicketAsync(database, room, "Deferred", 4, Now.AddMinutes(-2));
        await TicketAsync(database, room, "Finished", 5, Now);
        await TicketAsync(database, room, "Cancelled", 6, Now);
        await TicketAsync(database, Guid.NewGuid(), "Called", 7, Now);
        await TicketAsync(database, room, "Called", 8, Now.AddDays(-1));
        await TicketAsync(database, room, "Called", 9, Now.AddDays(1));

        var staff = await reader.GetStaffAsync(room, null, 1, 2, Ct);
        Assert.Equal(new DateOnly(2026, 11, 17), staff!.ServiceDate);
        Assert.True(staff.HasMore);
        Assert.Equal(new[] { 1, 2 }, staff.Items.Select(x => x.SequenceNumber));
        var second = await reader.GetStaffAsync(room, null, 2, 2, Ct);
        Assert.False(second!.HasMore);
        Assert.Equal(new[] { 3, 4 }, second.Items.Select(x => x.SequenceNumber));
        var display = await reader.GetPublicAsync(room, Ct);
        Assert.Equal(new[] { 3, 2 }, display.Tickets.Select(x => x.SequenceNumber));
        Assert.All(display.Tickets, t => Assert.Equal($"Q-{t.SequenceNumber:000}", t.TicketNumber));
    }

    [Fact]
    public async Task Doctor_can_read_only_a_room_assigned_in_todays_quota()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var services = ReaderServices(database);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IRoomQueueReader>();
        var room = Guid.NewGuid();
        var doctor = Guid.NewGuid();
        await database.InsertAsync("scheduling.DoctorDayQuota", new
        {
            DoctorId = doctor, AssignedRoomId = room, ServiceDate = new DateTime(2026, 11, 17), Capacity = 30,
            SessionStatus = "Scheduled", IsOpen = true,
        });
        Assert.NotNull(await reader.GetStaffAsync(room, doctor, 1, 50, Ct));
        Assert.Null(await reader.GetStaffAsync(Guid.NewGuid(), doctor, 1, 50, Ct));
        Assert.Null(await reader.GetStaffAsync(room, Guid.NewGuid(), 1, 50, Ct));
    }

    private static ServiceProvider ReaderServices(TestDatabase database)
    {
        var services = new ServiceCollection().AddSchedulingInfrastructure(TestDatabase.RequiredConnectionString());
        services.AddScoped(_ => database.CreateContext<SchedulingDbContext>(options => new SchedulingDbContext(options)));
        services.AddSingleton<IDbClock, QueueClock>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static Task<Guid> TicketAsync(TestDatabase database, Guid room, string status, int number, DateTime issued) =>
        database.InsertAsync("scheduling.QueueTicket", new
        {
            EncounterId = Guid.NewGuid(), TargetRoomId = room, TicketNumber = $"Q-{number:000}", SequenceNumber = number,
            TicketType = TicketType.InitialConsultation.ToString(), QueueStatus = status, IssuedAtUtc = issued,
            CalledAtUtc = status is "Called" or "InProgress" ? (DateTime?)issued : null,
        });

    private sealed class QueueClock : IDbClock
    {
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now);
    }
}
