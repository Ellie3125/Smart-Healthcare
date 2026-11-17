using System.Reflection;
using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure.Queues;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ReturnTicketActivationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Clinical_event_publishes_and_dispatches_under_the_existing_short_wire_name()
    {
        await using var f = await Fixture.Create();
        await using var scope = f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(f.Event, f.Encounter, t), Ct);
        var row = await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<OutboxMessage>().AsNoTracking().SingleAsync(x => x.Id == f.Event.EventId, Ct);
        Assert.Equal("ResultPublished", row.EventType.ToString()); Assert.Equal(f.Encounter, row.AggregateId);
        Assert.Equal("Encounter", row.AggregateType.ToString());
        var registration = Assert.Single(scope.ServiceProvider.GetServices<OutboxEventRegistration>());
        await registration.DispatchAsync(scope.ServiceProvider, row.PayloadJson, Ct);
        Assert.Equal(QueueStatus.Waiting, (await f.Ticket()).QueueStatus);
    }

    [Fact]
    public async Task Parallel_events_and_replay_activate_the_existing_ticket_once_and_keep_issue_time()
    {
        await using var f = await Fixture.Create(); var before = await f.Ticket();
        var other = f.Event with { EventId = Guid.NewGuid() }; f.Events.Add(other.EventId);
        await Task.WhenAll(f.Deliver(f.Event), f.Deliver(other), f.Deliver(f.Event));
        var after = await f.Ticket();
        Assert.Equal(QueueStatus.Waiting, after.QueueStatus); Assert.NotNull(after.ActivatedAtUtc);
        Assert.Equal(before.IssuedAtUtc, after.IssuedAtUtc); Assert.Equal(before.TicketNumber, after.TicketNumber);
        Assert.Equal(before.TargetRoomId, after.TargetRoomId); Assert.Equal(before.SequenceNumber, after.SequenceNumber);
        Assert.Equal(1, await f.CountAudit()); Assert.Equal(1, await f.CountOutbox());
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(1, await db.Set<QueueTicket>().CountAsync(x => x.EncounterId == f.Encounter, Ct));
    }

    [Theory]
    [InlineData("RequiredResultsNotPublished")]
    [InlineData("PatientBusy")]
    [InlineData("OriginalDoctorOrRoomUnavailable")]
    [InlineData("OriginalRoomMismatch")]
    [InlineData("StationDisabled")]
    [InlineData("RoomDisabled")]
    public async Task Deferred_cases_roll_back_the_marker_and_can_retry_after_the_source_is_fixed(string reason)
    {
        await using var f = await Fixture.Create();
        if (reason == "RequiredResultsNotPublished") f.Clinical.Published = false;
        if (reason == "OriginalDoctorOrRoomUnavailable") f.Identity.Active = false;
        if (reason == "RoomDisabled") f.Identity.RoomActive = false;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            if (reason == "PatientBusy")
            {
                var station = Guid.NewGuid(); var busy = Guid.NewGuid();
                db.Add(new ServiceStation { Id = station, RoomId = f.Room, StationCode = $"TEST-{station:N}", StationName = "Test", IsActive = true });
                db.Add(new QueueTicket { Id = busy, EncounterId = f.Encounter, TargetRoomId = f.Room, TargetStationId = station,
                    TicketNumber = $"TEST-{busy:N}", TicketType = TicketType.CLS, SequenceNumber = 1, QueueStatus = QueueStatus.InProgress, IssuedAtUtc = DateTime.UtcNow });
                db.Add(new ActivePatientEngagement { PatientId = f.Patient, EncounterId = f.Encounter, CurrentStationId = station,
                    CurrentTicketId = busy, AcquiredAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(Ct);
            }
            if (reason == "OriginalRoomMismatch") await db.Set<QueueTicket>().Where(x => x.Id == f.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TargetRoomId, Guid.NewGuid()), Ct);
            if (reason == "StationDisabled")
            {
                var station = Guid.NewGuid();
                db.Add(new ServiceStation { Id = station, RoomId = f.Room, StationCode = $"TEST-{station:N}", StationName = "Test", IsActive = false });
                await db.SaveChangesAsync(Ct);
            }
        }
        var expected = reason is "StationDisabled" or "RoomDisabled" ? "OriginalDoctorOrRoomUnavailable" : reason;
        Assert.Equal(expected, (await Assert.ThrowsAsync<ReturnTicketActivationDeferredException>(() => f.Deliver(f.Event))).Message);
        Assert.Equal(QueueStatus.Deferred, (await f.Ticket()).QueueStatus); Assert.Equal(0, await f.CountAudit());
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            await db.Set<ActivePatientEngagement>().Where(x => x.PatientId == f.Patient).ExecuteDeleteAsync(Ct);
            await db.Set<QueueTicket>().Where(x => x.Id == f.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.TargetRoomId, f.Room), Ct);
            await db.Set<ServiceStation>().Where(x => x.RoomId == f.Room).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), Ct);
        }
        f.Clinical.Published = true; f.Identity.Active = true; f.Identity.RoomActive = true; await f.Deliver(f.Event);
        Assert.Equal(QueueStatus.Waiting, (await f.Ticket()).QueueStatus); Assert.Equal(1, await f.CountOutbox());
    }

    [Theory]
    [InlineData("Superseded")]
    [InlineData("CancelledTicket")]
    [InlineData("CompletedEncounter")]
    [InlineData("AbandonedEncounter")]
    public async Task Late_event_never_reopens_a_superseded_or_final_journey(string reason)
    {
        await using var f = await Fixture.Create();
        if (reason == "Superseded") f.Clinical.Superseded = true;
        if (reason == "CompletedEncounter") f.Clinical.Status = "Completed";
        if (reason == "AbandonedEncounter") f.Clinical.Status = "Abandoned";
        if (reason == "CancelledTicket")
        {
            await using var scope = f.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>()
                .Set<QueueTicket>().Where(x => x.Id == f.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.QueueStatus, QueueStatus.Cancelled), Ct);
        }
        await f.Deliver(f.Event); Assert.Null((await f.Ticket()).ActivatedAtUtc); Assert.Equal(0, await f.CountOutbox());
    }

    [Fact]
    public async Task Publishing_failure_rolls_back_activation_audit_and_processed_marker()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var scope = f.Services.CreateAsyncScope();
            var handler = ActivatorUtilities.CreateInstance<ReturnTicketActivationHandler>(scope.ServiceProvider, new BrokenPublisher());
            await handler.HandleAsync(f.Event, Ct);
        });
        Assert.Equal(QueueStatus.Deferred, (await f.Ticket()).QueueStatus); Assert.Equal(0, await f.CountAudit());
        await f.Deliver(f.Event); Assert.Equal(1, await f.CountOutbox());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Id { get; } = Guid.NewGuid(); public Guid Encounter { get; } = Guid.NewGuid(); public Guid Patient { get; } = Guid.NewGuid();
        public Guid Room { get; } = Guid.NewGuid(); public Guid Doctor { get; } = Guid.NewGuid(); public Guid Order { get; } = Guid.NewGuid();
        public ServiceProvider Services { get; private set; } = null!; public ClinicalData Clinical { get; private set; } = null!;
        public IdentityData Identity { get; private set; } = null!; public List<Guid> Events { get; } = [];
        public ClsResultsPublishedIntegrationEvent Event { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var clinical = DispatchProxy.Create<IClinicalModule, ClinicalData>(); f.Clinical = (ClinicalData)clinical;
            f.Clinical.Encounter = new(f.Encounter, Guid.NewGuid(), f.Patient, f.Doctor, f.Room, "Test", "Test", DateTime.UtcNow, "InProgress", null);
            f.Clinical.Order = new(f.Order, f.Encounter, 1, DateTime.UtcNow, f.Doctor, "Confirmed", false,
                [new(Guid.NewGuid(), f.Order, Guid.NewGuid(), "Test", 100, "ServiceFinished", true, DateTime.UtcNow, null)]);
            var identity = DispatchProxy.Create<IIdentityPatientsModule, IdentityData>(); f.Identity = (IdentityData)identity;
            var cs = TestDatabase.RequiredConnectionString(); f.Services = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
                .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton(clinical).AddSingleton(identity)
                .AddOutboxEvent<ClsResultsPublishedIntegrationEvent>(Shared.Infrastructure.Persistence.Enums.EventType.ResultPublished).BuildServiceProvider();
            f.Event = new(Guid.NewGuid(), f.Encounter, f.Order, DateTime.UtcNow, null); f.Events.Add(f.Event.EventId);
            await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.Add(new QueueTicket { Id = f.Id, EncounterId = f.Encounter, ServiceOrderId = f.Order, TargetRoomId = f.Room, TicketNumber = $"TEST-{f.Id:N}",
                SequenceNumber = 1, TicketType = TicketType.ReturnConsultation, QueueStatus = QueueStatus.Deferred, IssuedAtUtc = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync(Ct); return f;
        }
        public async Task Deliver(ClsResultsPublishedIntegrationEvent e)
        { await using var s = Services.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<IIntegrationEventHandler<ClsResultsPublishedIntegrationEvent>>().HandleAsync(e, Ct); }
        public async Task<QueueTicket> Ticket()
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().AsNoTracking().SingleAsync(x => x.Id == Id, Ct); }
        public async Task<int> CountAudit()
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().CountAsync(x => x.RecordId == Id, Ct); }
        public async Task<int> CountOutbox()
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<SharedDbContext>().Set<OutboxMessage>().CountAsync(x => x.AggregateId == Id, Ct); }
        public async ValueTask DisposeAsync()
        {
            await using var s = Services.CreateAsyncScope(); var shared = s.ServiceProvider.GetRequiredService<SharedDbContext>(); var db = s.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            await shared.Set<ProcessedEvent>().Where(x => Events.Contains(x.EventId)).ExecuteDeleteAsync(Ct);
            await shared.Set<OutboxMessage>().Where(x => x.AggregateId == Id || Events.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(x => x.RecordId == Id).ExecuteDeleteAsync(Ct);
            await db.Set<ActivePatientEngagement>().Where(x => x.PatientId == Patient).ExecuteDeleteAsync(Ct);
            await db.Set<QueueTicket>().Where(x => x.EncounterId == Encounter).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceStation>().Where(x => x.RoomId == Room).ExecuteDeleteAsync(Ct); await Services.DisposeAsync();
        }
    }
    public class ClinicalData : DispatchProxy
    {
        public EncounterDto Encounter { get; set; } = null!; public ServiceOrderDto Order { get; set; } = null!;
        public bool Published { get; set; } = true; public bool Superseded { get; set; } public string Status { get; set; } = "InProgress";
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IClinicalModule.GetEncounterByIdAsync)) return Task.FromResult<EncounterDto?>(Encounter with { Status = Status });
            if (method?.Name != nameof(IClinicalModule.GetServiceOrdersByEncounterIdAsync)) throw new NotSupportedException(method?.Name);
            var item = Order.Items.Single(); var order = Order with { Items = [item with { Result = Published
                ? new(Guid.NewGuid(), item.Id, "", "", "Published", null, null, null, null, DateTime.UtcNow) : null }] };
            IReadOnlyList<ServiceOrderDto> orders = Superseded ? [order, order with { Id = Guid.NewGuid(), OrderRoundNumber = 2 }] : [order];
            return Task.FromResult(orders);
        }
    }
    public class IdentityData : DispatchProxy
    {
        public bool Active { get; set; } = true; public bool RoomActive { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IIdentityPatientsModule.GetDoctorAsync) => Task.FromResult<DoctorDto?>(new((Guid)args![0]!, "Test", Guid.NewGuid(), "Test", Guid.NewGuid(), "Test", Active)),
            nameof(IIdentityPatientsModule.GetRoomAsync) => Task.FromResult<RoomDto?>(new((Guid)args![0]!, "TEST", "Test", "Clinical", RoomActive)),
            _ => throw new NotSupportedException(method?.Name)
        };
    }
    private sealed class BrokenPublisher : IIntegrationEventPublisher
    { public Task PublishAsync<T>(T e, Guid aggregateId, CancellationToken ct) where T : IIntegrationEvent => throw new InvalidOperationException("Publisher failed"); }
}
