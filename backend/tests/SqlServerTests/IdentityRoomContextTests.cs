using System.Text.Json;
using IdentityPatients.Contracts;
using IdentityPatients.Domain.Entities;
using IdentityPatients.Domain.Enums;
using IdentityPatients.Infrastructure;
using IdentityPatients.Infrastructure.Persistence;
using IdentityPatients.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application.Queues;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class IdentityRoomContextTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("User")]
    [InlineData("Account")]
    [InlineData("Room")]
    [InlineData("Leader")]
    [InlineData("LeaderUser")]
    public async Task Inactive_actor_or_shift_owner_cannot_supply_a_verified_context(string target)
    {
        await using var f = await RoomAuditFixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
        if (target == "User" || target == "LeaderUser") await db.AppUsers.Where(x => x.Id == (target == "User" ? f.UserId : f.LeaderUserId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        else if (target == "Account") await db.RoomAccounts.Where(x => x.Id == f.AccountId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        else if (target == "Room") await db.Rooms.Where(x => x.Id == f.RoomId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        else await db.Staffs.Where(x => x.Id == f.LeaderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        Assert.Null(await f.ReadAsync());
    }
    [Fact]
    public async Task Ambiguous_shift_leader_and_non_utc_clock_are_rejected()
    {
        await using var f = await RoomAuditFixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => f.ReadAsync(at: DateTime.SpecifyKind(RoomAuditFixture.Now, DateTimeKind.Unspecified)));
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
        db.Add(new StaffShiftAssignment { Id = Guid.NewGuid(), StaffId = f.LeaderId, RoomId = f.RoomId,
            ShiftDate = DateOnly.FromDateTime(RoomAuditFixture.Now.AddHours(7)), ShiftName = ShiftName.FullDay,
            StartsAtUtc = RoomAuditFixture.Now.AddHours(-1), EndsAtUtc = RoomAuditFixture.Now.AddHours(1), IsShiftLeader = true });
        await db.SaveChangesAsync(Ct);
        Assert.Null(await f.ReadAsync());
    }
    [Fact]
    public async Task Reader_verifies_session_room_and_current_shift_and_fails_closed_for_missing_or_ambiguous_context()
    {
        await using var f = await RoomAuditFixture.CreateAsync();
        var result = await f.ReadAsync();
        Assert.NotNull(result);
        Assert.Equal(f.SessionId, result.RoomDeviceSessionId);
        Assert.Equal(f.AssignmentId, result.StaffShiftAssignmentId);
        Assert.Equal(f.LeaderId, result.ShiftLeaderStaffId);
        Assert.Null(await f.ReadAsync(token: "wrong-token"));
        Assert.Null(await f.ReadAsync(room: Guid.NewGuid()));
        Assert.Null(await f.ReadAsync(at: RoomAuditFixture.Now.AddDays(1)));
        await using var scope = f.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IIdentityPatientsModule>()
            .GetRoomOperationContextAsync(f.UserId, f.RoomId, f.Token, RoomAuditFixture.Now, Ct));
        await scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>().RoomDeviceSessions.Where(x => x.Id == f.SessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        Assert.Null(await f.ReadAsync());
    }
    [Fact]
    public async Task Room_transition_records_verified_context_and_a_real_operation_without_token_data()
    {
        await using var f = await RoomAuditFixture.CreateAsync();
        await f.StartAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().SingleAsync(x => x.RecordId == f.TicketId, Ct);
        Assert.Equal(f.UserId, audit.PerformedByUserId);
        Assert.Equal(f.AccountId, audit.RoomAccountId);
        Assert.Equal(f.SessionId, audit.RoomDeviceSessionId);
        Assert.Equal(f.AssignmentId, audit.StaffShiftAssignmentId);
        Assert.Equal("registered-device", audit.DeviceId);
        Assert.NotNull(audit.OperationId);
        var operation = await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<Notifications.Domain.Entities.Operation>()
            .SingleAsync(x => x.Id == audit.OperationId, Ct);
        Assert.Equal(Notifications.Domain.Enums.OperationStatus.Succeeded, operation.Status);
        Assert.Equal(f.TicketId, operation.TargetEntityId);
        Assert.DoesNotContain(f.Token, audit.NewValuesJson!);
        Assert.Contains(f.LeaderId.ToString(), audit.NewValuesJson!);
    }
    [Fact]
    public async Task Missing_session_and_caller_failure_roll_back_ticket_audit_and_operation()
    {
        await using var f = await RoomAuditFixture.CreateAsync();
        await Assert.ThrowsAsync<RoomAuditDeniedException>(() => f.StartAsync(token: "invalid"));
        await Assert.ThrowsAsync<Rollback>(() => f.StartAsync(fail: true));
        await using var scope = f.Services.CreateAsyncScope();
        Assert.Equal(QueueStatus.Called, (await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().SingleAsync(x => x.Id == f.TicketId, Ct)).QueueStatus);
        Assert.False(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().AnyAsync(x => x.RecordId == f.TicketId, Ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<Notifications.Domain.Entities.Operation>().AnyAsync(x => x.TargetEntityId == f.TicketId, Ct));
    }
    internal sealed class Rollback : Exception;
}

internal sealed class RoomAuditFixture(ServiceProvider services) : IAsyncDisposable
{
    public ServiceProvider Services { get; } = services;
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid LeaderUserId { get; } = Guid.NewGuid();
    public Guid LeaderId { get; } = Guid.NewGuid();
    public Guid RoomId { get; } = Guid.NewGuid();
    public Guid AccountId { get; } = Guid.NewGuid();
    public Guid SessionId { get; } = Guid.NewGuid();
    public Guid AssignmentId { get; } = Guid.NewGuid();
    public Guid TicketId { get; } = Guid.NewGuid();
    public Guid StationId { get; } = Guid.NewGuid();
    public string Token { get; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public static DateTime Now => new(2026, 11, 18, 2, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public static async Task<RoomAuditFixture> CreateAsync()
    {
        var cs = TestDatabase.RequiredConnectionString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["Jwt:Issuer"] = "Room.Audit.Tests", ["Jwt:Audience"] = "Room.Audit.Tests" }).Build();
        var services = new ServiceCollection().AddSharedInfrastructure(cs).AddIdentityPatientsInfrastructure(configuration, cs)
            .AddSchedulingInfrastructure(cs).AddNotificationsInfrastructure(cs).AddSingleton<IDbClock>(new Clock())
            .AddUnitOfWorkParticipant<IdentityPatientsDbContext>().AddUnitOfWorkParticipant<SchedulingDbContext>().AddUnitOfWorkParticipant<WorkflowDbContext>()
            // Hàng đợi cần Billing để kiểm phí CLS; test này chỉ dùng vé khám đầu nên Billing không được gọi.
            .AddSingleton(System.Reflection.DispatchProxy.Create<Billing.Contracts.IBillingModule, NoBilling>())
            .BuildServiceProvider();
        var f = new RoomAuditFixture(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
        db.AddRange(new AppUser { Id = f.UserId, UserName = "room-" + f.UserId.ToString("N"), PasswordHash = "fixture", IsActive = true, CreatedAtUtc = Now },
            new AppUser { Id = f.LeaderUserId, UserName = "leader-" + f.LeaderUserId.ToString("N"), PasswordHash = "fixture", IsActive = true, CreatedAtUtc = Now },
            new Room { Id = f.RoomId, RoomNumber = f.RoomId.ToString("N")[..12], RoomName = "Test", RoomType = RoomType.Laboratory, DepartmentName = "Test", IsActive = true },
            new Staff { Id = f.LeaderId, AppUserId = f.LeaderUserId, StaffCode = f.LeaderId.ToString("N"), FullName = "Test leader", StaffType = StaffType.Doctor, IsActive = true, CreatedAtUtc = Now },
            new RoomAccount { Id = f.AccountId, AppUserId = f.UserId, RoomId = f.RoomId, AccountCode = f.AccountId.ToString("N"), IsActive = true, CreatedAtUtc = Now },
            new RoomDeviceSession { Id = f.SessionId, RoomAccountId = f.AccountId, DeviceId = "registered-device", DeviceIpAddress = "127.0.0.1", SessionToken = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(f.Token))).ToLowerInvariant(), IsActive = true, LoginAtUtc = Now },
            new StaffShiftAssignment { Id = f.AssignmentId, StaffId = f.LeaderId, RoomId = f.RoomId, ShiftDate = DateOnly.FromDateTime(Now.AddHours(7)), ShiftName = ShiftName.Morning, StartsAtUtc = Now.AddHours(-1), EndsAtUtc = Now.AddHours(1), IsShiftLeader = true });
        await db.SaveChangesAsync(Ct);
        var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var patient = Guid.NewGuid(); var encounter = Guid.NewGuid();
        scheduling.AddRange(new ServiceStation { Id = f.StationId, RoomId = f.RoomId, StationCode = f.StationId.ToString("N"), StationName = "Test", IsActive = true },
            new QueueTicket { Id = f.TicketId, EncounterId = encounter, TargetRoomId = f.RoomId, TargetStationId = f.StationId,
                TicketNumber = f.TicketId.ToString("N"), TicketType = TicketType.InitialConsultation, SequenceNumber = 1, QueueStatus = QueueStatus.Called, IssuedAtUtc = Now },
            new ActivePatientEngagement { PatientId = patient, EncounterId = encounter, CurrentStationId = f.StationId, CurrentTicketId = f.TicketId, AcquiredAtUtc = Now },
            new ActiveStationAllocation { StationId = f.StationId, CurrentPatientId = patient, CurrentTicketId = f.TicketId, AllocatedAtUtc = Now });
        await scheduling.SaveChangesAsync(Ct);
        return f;
    }
    public async Task<RoomOperationContext?> ReadAsync(string? token = null, Guid? room = null, DateTime? at = null)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<IIdentityPatientsModule>()
            .GetRoomOperationContextAsync(UserId, room ?? RoomId, token ?? Token, at ?? Now, t), Ct);
    }
    public async Task StartAsync(string? token = null, bool fail = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var request = scope.ServiceProvider.GetRequiredService<RoomAuditRequest>();
        request.IsRoomAccount = true; request.UserId = UserId; request.RoomId = RoomId; request.SessionToken = token ?? Token;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(async t =>
        {
            var result = await scope.ServiceProvider.GetRequiredService<IQueueStore>().TransitionAsync(new(TicketId, UserId, RoomId), TicketTransition.Start, t);
            Assert.Equal(QueueResult.Called, result.Result);
            if (fail) throw new IdentityRoomContextTests.Rollback();
        }, Ct);
    }
    public async ValueTask DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            await shared.Set<AuditLog>().Where(x => x.RecordId == TicketId).ExecuteDeleteAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<Notifications.Domain.Entities.Operation>().Where(x => x.TargetEntityId == TicketId).ExecuteDeleteAsync(Ct);
            var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            await scheduling.Set<ActivePatientEngagement>().Where(x => x.CurrentTicketId == TicketId).ExecuteDeleteAsync(Ct);
            await scheduling.Set<ActiveStationAllocation>().Where(x => x.StationId == StationId).ExecuteDeleteAsync(Ct);
            await scheduling.Set<QueueTicket>().Where(x => x.Id == TicketId).ExecuteDeleteAsync(Ct);
            await scheduling.Set<ServiceStation>().Where(x => x.Id == StationId).ExecuteDeleteAsync(Ct);
            var identity = scope.ServiceProvider.GetRequiredService<IdentityPatientsDbContext>();
            await identity.StaffShiftAssignments.Where(x => x.RoomId == RoomId).ExecuteDeleteAsync(Ct);
            await identity.RoomDeviceSessions.Where(x => x.Id == SessionId).ExecuteDeleteAsync(Ct);
            await identity.RoomAccounts.Where(x => x.Id == AccountId).ExecuteDeleteAsync(Ct);
            await identity.Staffs.Where(x => x.Id == LeaderId).ExecuteDeleteAsync(Ct);
            await identity.Rooms.Where(x => x.Id == RoomId).ExecuteDeleteAsync(Ct);
            await identity.AppUsers.Where(x => x.Id == UserId || x.Id == LeaderUserId).ExecuteDeleteAsync(Ct);
        }
        await Services.DisposeAsync();
    }
    private sealed class Clock : IDbClock { public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now); }
}

public class NoBilling : System.Reflection.DispatchProxy
{
    protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => throw new NotSupportedException(method?.Name);
}
