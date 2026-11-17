using System.Reflection;
using Billing.Contracts;
using Clinical.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.Queues;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure.Queues;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClsDayEndTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Midnight_in_vietnam_is_the_boundary_and_parallel_replays_cancel_once()
    {
        await using var f = await Fixture.Create(); f.Clock.Now = f.Clock.Now.AddSeconds(-1);
        async Task<int> Run()
        { await using var scope = f.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ClsDayEndJob>().RunAsync(Ct); }
        Assert.Equal(0, await Run()); f.Clock.Now = f.Clock.Now.AddSeconds(1);
        var batch = Run(); var replay = f.Cancel(); await Task.WhenAll(batch, replay);
        Assert.Equal(1, await batch + (await replay ? 1 : 0));
        Assert.Equal(QueueStatus.Cancelled, (await f.Read()).QueueStatus);
        Assert.Equal((1, 1), await f.Evidence());
    }

    [Theory]
    [InlineData(QueueStatus.Waiting, true)]
    [InlineData(QueueStatus.Called, true)]
    [InlineData(QueueStatus.InProgress, false)]
    [InlineData(QueueStatus.Finished, false)]
    [InlineData(QueueStatus.Cancelled, false)]
    public async Task Only_unperformed_cls_tickets_are_cancelled_and_only_their_allocation_is_released(QueueStatus status, bool cancelled)
    {
        await using var f = await Fixture.Create(status); Assert.Equal(cancelled, await f.Cancel());
        Assert.Equal(cancelled ? QueueStatus.Cancelled : status, (await f.Read()).QueueStatus);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(status == QueueStatus.InProgress ? 1 : 0, await db.Set<ActivePatientEngagement>().CountAsync(x => x.PatientId == f.Patient, Ct));
        Assert.Equal(status == QueueStatus.InProgress ? 1 : 0, await db.Set<ActiveStationAllocation>().CountAsync(x => x.StationId == f.Station, Ct));
    }

    [Theory]
    [InlineData("SampleCollected")]
    [InlineData("InProgress")]
    [InlineData("ServiceFinished")]
    [InlineData("Completed")]
    public async Task Authoritative_clinical_work_already_started_is_never_cancelled_even_if_queue_state_is_stale(string status)
    { await using var f = await Fixture.Create(); f.Source.ItemStatus = status; Assert.False(await f.Cancel()); Assert.Equal(QueueStatus.Waiting, (await f.Read()).QueueStatus); }

    [Theory]
    [InlineData(TicketType.InitialConsultation)]
    [InlineData(TicketType.ReturnConsultation)]
    public async Task Other_ticket_types_are_untouched(TicketType type)
    { await using var f = await Fixture.Create(type: type); Assert.False(await f.Cancel()); }

    [Fact]
    public async Task Cancelling_a_waiting_ticket_preserves_the_patients_allocation_at_another_service()
    {
        await using var f = await Fixture.Create(); var other = Guid.NewGuid();
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.Add(new QueueTicket { Id = other, EncounterId = f.Encounter, TargetRoomId = f.Room, TargetStationId = f.Station,
                ServiceOrderItemId = Guid.NewGuid(), TicketNumber = $"TEST-{other:N}", TicketType = TicketType.CLS, SequenceNumber = 2,
                QueueStatus = QueueStatus.InProgress, IssuedAtUtc = f.Clock.Now.AddHours(-1) });
            db.Add(new ActivePatientEngagement { PatientId = f.Patient, EncounterId = f.Encounter, CurrentStationId = f.Station, CurrentTicketId = other, AcquiredAtUtc = f.Clock.Now });
            db.Add(new ActiveStationAllocation { StationId = f.Station, CurrentPatientId = f.Patient, CurrentTicketId = other, AllocatedAtUtc = f.Clock.Now });
            await db.SaveChangesAsync(Ct);
        }
        Assert.True(await f.Cancel());
        await using var check = f.Services.CreateAsyncScope(); var data = check.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(1, await data.Set<ActivePatientEngagement>().CountAsync(x => x.CurrentTicketId == other, Ct));
        Assert.Equal(1, await data.Set<ActiveStationAllocation>().CountAsync(x => x.CurrentTicketId == other, Ct));
    }

    [Fact]
    public async Task Today_and_missing_clinical_source_are_not_cancelled()
    {
        await using var f = await Fixture.Create();
        await using (var scope = f.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>()
            .Where(x => x.Id == f.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IssuedAtUtc, f.Clock.Now), Ct);
        Assert.False(await f.Cancel()); f.Clock.Now = f.Clock.Now.AddDays(1); f.Source.Missing = true;
        Assert.False(await f.Cancel()); Assert.Equal((0, 0), await f.Evidence());
    }

    [Fact]
    public async Task Start_and_day_end_cancel_have_one_winner_and_keep_resource_state_consistent()
    {
        await using var f = await Fixture.Create(QueueStatus.Called);
        async Task<QueueOutcome> Start()
        {
            await using var scope = f.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t =>
                scope.ServiceProvider.GetRequiredService<IQueueStore>().TransitionAsync(new(f.Id, Guid.NewGuid(), f.Room), TicketTransition.Start, t), Ct);
        }
        var start = Start(); var cancel = f.Cancel(); await Task.WhenAll(start, cancel);
        var status = (await f.Read()).QueueStatus;
        Assert.True(status is QueueStatus.InProgress or QueueStatus.Cancelled);
        Assert.Equal(status == QueueStatus.Cancelled, await cancel);
        await using var scope = f.Services.CreateAsyncScope();
        Assert.Equal(status == QueueStatus.InProgress ? 1 : 0, await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>()
            .Set<ActiveStationAllocation>().CountAsync(x => x.CurrentTicketId == f.Id, Ct));
    }

    [Fact]
    public async Task Outbox_failure_rolls_back_ticket_audit_and_allocation_release()
    {
        await using var f = await Fixture.Create(QueueStatus.Called);
        await using (var scope = f.Services.CreateAsyncScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => ActivatorUtilities.CreateInstance<ClsDayEndJob>(scope.ServiceProvider, new BrokenPublisher()).TryCancelAsync(f.Id, Ct));
        Assert.Equal(QueueStatus.Called, (await f.Read()).QueueStatus); Assert.Equal((0, 0), await f.Evidence());
        Assert.True(await f.Cancel());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid(); public Guid Encounter { get; } = Guid.NewGuid(); public Guid Patient { get; } = Guid.NewGuid();
        public Guid Room { get; } = Guid.NewGuid(); public Guid Station { get; } = Guid.NewGuid(); public Guid Item { get; } = Guid.NewGuid();
        public FixedClock Clock { get; } = new(); public ServiceProvider Services { get; private set; } = null!; public SourceData Source { get; private set; } = null!;
        public static async Task<Fixture> Create(QueueStatus status = QueueStatus.Waiting, TicketType type = TicketType.CLS)
        {
            var f = new Fixture(); var clinical = DispatchProxy.Create<IClinicalModule, SourceData>(); f.Source = (SourceData)clinical;
            f.Source.Encounter = new(f.Encounter, Guid.NewGuid(), f.Patient, Guid.NewGuid(), f.Room, "Test", "Test", f.Clock.Now.AddHours(-1), "WaitingCLS", null);
            f.Source.Item = f.Item; var billing = DispatchProxy.Create<IBillingModule, PaidFees>(); ((PaidFees)billing).Encounter = f.Encounter; ((PaidFees)billing).Item = f.Item;
            var cs = TestDatabase.RequiredConnectionString(); f.Services = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
                .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton(clinical).AddSingleton(billing).AddSingleton<IDbClock>(f.Clock).BuildServiceProvider();
            await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.Add(new ServiceStation { Id = f.Station, RoomId = f.Room, StationCode = $"TEST-{f.Station:N}", StationName = "Test", IsActive = true });
            db.Add(new QueueTicket { Id = f.Id, EncounterId = f.Encounter, TargetRoomId = f.Room, TargetStationId = status is QueueStatus.Called or QueueStatus.InProgress ? f.Station : null,
                ServiceOrderId = type == TicketType.ReturnConsultation ? Guid.NewGuid() : null, ServiceOrderItemId = type == TicketType.CLS ? f.Item : null,
                TicketNumber = $"TEST-{f.Id:N}", TicketType = type, SequenceNumber = 1, QueueStatus = status, IssuedAtUtc = f.Clock.Now.AddSeconds(-1) });
            if (status is QueueStatus.Called or QueueStatus.InProgress)
            {
                db.Add(new ActivePatientEngagement { PatientId = f.Patient, EncounterId = f.Encounter, CurrentStationId = f.Station, CurrentTicketId = f.Id, AcquiredAtUtc = f.Clock.Now });
                db.Add(new ActiveStationAllocation { StationId = f.Station, CurrentPatientId = f.Patient, CurrentTicketId = f.Id, AllocatedAtUtc = f.Clock.Now });
            }
            await db.SaveChangesAsync(Ct); return f;
        }
        public async Task<bool> Cancel()
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ClsDayEndJob>().TryCancelAsync(Id, Ct); }
        public async Task<QueueTicket> Read()
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().AsNoTracking().SingleAsync(x => x.Id == Id, Ct); }
        public async Task<(int Audit, int Outbox)> Evidence()
        {
            await using var s = Services.CreateAsyncScope(); var db = s.ServiceProvider.GetRequiredService<SharedDbContext>();
            return (await db.Set<AuditLog>().CountAsync(x => x.RecordId == Id, Ct), await db.Set<OutboxMessage>().CountAsync(x => x.AggregateId == Id, Ct));
        }
        public async ValueTask DisposeAsync()
        {
            await using var s = Services.CreateAsyncScope(); var db = s.ServiceProvider.GetRequiredService<SchedulingDbContext>(); var shared = s.ServiceProvider.GetRequiredService<SharedDbContext>();
            var ids = await db.Set<QueueTicket>().Where(x => x.EncounterId == Encounter).Select(x => x.Id).ToListAsync(Ct);
            await db.Set<ActivePatientEngagement>().Where(x => ids.Contains(x.CurrentTicketId)).ExecuteDeleteAsync(Ct);
            await db.Set<ActiveStationAllocation>().Where(x => ids.Contains(x.CurrentTicketId)).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(x => x.RecordId == Id).ExecuteDeleteAsync(Ct);
            await shared.Set<OutboxMessage>().Where(x => x.AggregateId == Id).ExecuteDeleteAsync(Ct);
            await db.Set<QueueTicket>().Where(x => x.EncounterId == Encounter).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceStation>().Where(x => x.Id == Station).ExecuteDeleteAsync(Ct); await Services.DisposeAsync();
        }
    }
    private sealed class FixedClock : IDbClock
    { public DateTime Now { get; set; } = new(2026, 11, 17, 17, 0, 0, DateTimeKind.Utc); public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now); }
    public class SourceData : DispatchProxy
    {
        public EncounterDto Encounter { get; set; } = null!; public Guid Item { get; set; } public string ItemStatus { get; set; } = "Ordered"; public bool Missing { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IClinicalModule.GetEncounterByIdAsync)) return Task.FromResult<EncounterDto?>(!Missing && (Guid)args![0]! == Encounter.Id ? Encounter : null);
            if (method?.Name != nameof(IClinicalModule.GetServiceOrdersByEncounterIdAsync)) throw new NotSupportedException(method?.Name);
            var order = Guid.NewGuid(); IReadOnlyList<ServiceOrderDto> orders = [new(order, Encounter.Id, 1, DateTime.UtcNow, Encounter.OriginalDoctorId, "Confirmed", false,
                [new(Item, order, Guid.NewGuid(), "Test", 100, ItemStatus, true, null, null)])]; return Task.FromResult(orders);
        }
    }
    public class PaidFees : DispatchProxy
    {
        public Guid Encounter { get; set; } public Guid Item { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IBillingModule.GetEncounterCostSummaryAsync)
            ? Task.FromResult(new EncounterCostSummary(Encounter, "VND", 100, 100, 0, 100, 0, 0,
                [new(Guid.NewGuid(), ChargeType.CLSService, Guid.NewGuid(), "Test", 100, 1, 100, 100, 0, "VND", ChargeStatus.Paid, Encounter, null, Item)]))
            : throw new NotSupportedException(method?.Name);
    }
    private sealed class BrokenPublisher : IIntegrationEventPublisher
    { public Task PublishAsync<T>(T e, Guid aggregateId, CancellationToken ct) where T : IIntegrationEvent => throw new InvalidOperationException("Publisher failed"); }
}
