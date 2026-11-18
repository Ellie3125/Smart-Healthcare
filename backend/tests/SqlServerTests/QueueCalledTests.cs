using System.Collections.Concurrent;
using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Contracts;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Notifications.Domain.Entities;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

public sealed partial class SchedulingHoldTests
{
    [Fact]
    public async Task Parallel_call_replays_publish_once_and_a_real_recall_has_a_new_event()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock); var station = Guid.NewGuid();
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, [station]);
            var ticket = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            var results = await Task.WhenAll(CallNextAsync(services, day, station), CallNextAsync(services, day, station), CallNextAsync(services, day, station));
            Assert.All(results, r => Assert.Equal(ticket.QueueTicketId, r.Ticket!.QueueTicketId));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId='{ticket.QueueTicketId}' AND EventType='QueueCalled'"));
            await using (var scope = services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<QueueService>().RequeueAsync(new(ticket.QueueTicketId, Guid.NewGuid(), day.AssignedRoomId), Ct);
            Assert.Equal(ticket.QueueTicketId, (await CallNextAsync(services, day, station)).Ticket!.QueueTicketId);
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId='{ticket.QueueTicketId}' AND EventType='QueueCalled'"));
        }
        finally { await CleanupQueueAsync(day); }
    }

    [Fact]
    public async Task Failed_queue_called_publication_rolls_back_called_state_allocation_and_audit()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock); var station = Guid.NewGuid();
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, [station]);
            var ticket = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            await using (var scope = services.CreateAsyncScope())
            {
                var type = scope.ServiceProvider.GetRequiredService<IQueueStore>().GetType();
                var failing = (IQueueStore)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type, new BrokenQueueCalledPublisher());
                await Assert.ThrowsAsync<InvalidOperationException>(() => Uow(scope).ExecuteAsync(t => failing.TryAllocateAsync(
                    new(station, day.AssignedRoomId, ticket.QueueTicketId, ticket.PatientId, ticket.EncounterId, Guid.NewGuid()), t), Ct));
            }
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE Id='{ticket.QueueTicketId}' AND QueueStatus='Waiting'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveStationAllocation WHERE CurrentTicketId='{ticket.QueueTicketId}'"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM shared.AuditLog WHERE RecordId='{ticket.QueueTicketId}'"));
            Assert.Equal(ticket.QueueTicketId, (await CallNextAsync(services, day, station)).Ticket!.QueueTicketId);
        }
        finally { await CleanupQueueAsync(day); }
    }
    private sealed class BrokenQueueCalledPublisher : IIntegrationEventPublisher
    { public Task PublishAsync<T>(T e, Guid aggregateId, CancellationToken ct) where T : IIntegrationEvent => throw new InvalidOperationException("Publisher failed"); }
}

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class QueueCalledRealtimeTests
{
    [Fact]
    public async Task Duplicate_handlers_commit_the_marker_then_send_only_ticket_numbers_to_public_display()
    {
        var ct = TestContext.Current.CancellationToken;
        var cs = TestDatabase.RequiredConnectionString();
        var audience = new Audience();
        await using var services = new ServiceCollection().AddSharedInfrastructure(cs).AddNotificationsInfrastructure(cs)
            .AddUnitOfWorkParticipant<WorkflowDbContext>().AddSingleton<IIdentityPatientsModule>(audience).BuildServiceProvider();
        var e = new QueueCalled(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "TEST-001", 1, "CLS", Guid.NewGuid(), DateTime.UtcNow, null);
        var sends = new ConcurrentQueue<Delivery>();
        try
        {
            async Task Deliver()
            {
                await using var scope = services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
                var handler = ActivatorUtilities.CreateInstance<QueueCalledRealtimeHandler>(scope.ServiceProvider, new Recorder(db, sends));
                await handler.HandleAsync(e, ct);
            }
            await Task.WhenAll(Deliver(), Deliver(), Deliver());
            Assert.Equal(5, sends.Count);
            Assert.Equal(audience.Ids.Order(), sends.Where(s => s.Target == "patient").Select(s => s.Recipient).Order());
            Assert.All(sends.Where(s => s.Target == "patient"), s =>
            {
                Assert.Equal(RealtimeEvents.QueueCalled, s.EventName);
                Assert.Equal(new[] { "calledAtUtc", "eventId", "queueTicketId", "roomId", "ticketNumber" },
                    s.Payload.EnumerateObject().Select(p => p.Name).Order().ToArray());
                Assert.Equal(e.QueueTicketId, s.Payload.GetProperty("queueTicketId").GetGuid());
            });
            var display = Assert.Single(sends, s => s.Target == "public");
            Assert.Equal(e.RoomId, display.Recipient); Assert.Equal(RealtimeEvents.DisplayBoardUpdated, display.EventName);
            Assert.Equal(new[] { "sequenceNumber", "ticketNumber" }, display.Payload.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.Equal(e.TicketNumber, display.Payload.GetProperty("ticketNumber").GetString());
            Assert.Equal(e.PerformedByUserId, Assert.Single(sends, s => s.Target == "user").Recipient);
            Assert.Equal(e.RoomId, Assert.Single(sends, s => s.Target == "room").Recipient);
            await using var check = services.CreateAsyncScope();
            Assert.Equal(1, await check.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ProcessedEvent>().CountAsync(x => x.EventId == e.EventId, ct));
            var notification = Assert.Single(await check.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<PatientNotification>()
                .Where(x => x.SourceEventId == e.EventId).ToListAsync(ct));
            Assert.Equal(e.PatientId, notification.PatientId); Assert.Equal(e.QueueTicketId, notification.TargetEntityId);
            Assert.Equal(Notifications.Domain.Enums.PatientNotificationKind.TicketCalled, notification.Kind);
            Assert.Equal(e.PatientId, audience.PatientId);
        }
        finally
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ProcessedEvent>().Where(x => x.EventId == e.EventId).ExecuteDeleteAsync(ct);
            await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<PatientNotification>().Where(x => x.SourceEventId == e.EventId).ExecuteDeleteAsync(ct);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Identity_failure_rolls_back_marker_and_ticket_notification_then_retry_delivers_to_current_audience(bool hasAudience)
    {
        var ct = TestContext.Current.CancellationToken; var cs = TestDatabase.RequiredConnectionString();
        var audience = new Audience { Fail = true, Ids = hasAudience ? [Guid.NewGuid()] : [] };
        await using var services = new ServiceCollection().AddSharedInfrastructure(cs).AddNotificationsInfrastructure(cs)
            .AddUnitOfWorkParticipant<WorkflowDbContext>().AddSingleton<IIdentityPatientsModule>(audience).BuildServiceProvider();
        var e = new QueueCalled(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "TEST-002", 2, "FirstVisit", Guid.NewGuid(), DateTime.UtcNow, null);
        var sends = new ConcurrentQueue<Delivery>();
        async Task Deliver()
        {
            await using var scope = services.CreateAsyncScope();
            await ActivatorUtilities.CreateInstance<QueueCalledRealtimeHandler>(scope.ServiceProvider,
                new Recorder(scope.ServiceProvider.GetRequiredService<SharedDbContext>(), sends)).HandleAsync(e, ct);
        }
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(Deliver);
            await using (var check = services.CreateAsyncScope())
            {
                Assert.False(await check.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ProcessedEvent>().AnyAsync(x => x.EventId == e.EventId, ct));
                Assert.False(await check.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<PatientNotification>().AnyAsync(x => x.SourceEventId == e.EventId, ct));
            }
            Assert.Empty(sends); audience.Fail = false; await Deliver();
            Assert.Equal(hasAudience ? 4 : 3, sends.Count);
            await Deliver(); Assert.Equal(hasAudience ? 4 : 3, sends.Count);
        }
        finally
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ProcessedEvent>().Where(x => x.EventId == e.EventId).ExecuteDeleteAsync(ct);
            await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Set<PatientNotification>().Where(x => x.SourceEventId == e.EventId).ExecuteDeleteAsync(ct);
        }
    }

    private sealed class Audience : IIdentityPatientsModule
    {
        public Guid[] Ids { get; init; } = [Guid.NewGuid(), Guid.NewGuid()]; public Guid PatientId { get; private set; } public bool Fail { get; set; }
        public Task<IReadOnlyList<Guid>> GetPatientAppUserIdsAsync(Guid patientId, CancellationToken ct = default)
        { PatientId = patientId; if (Fail) throw new InvalidOperationException("Identity unavailable"); return Task.FromResult<IReadOnlyList<Guid>>(Ids); }
        public Task<bool> CanAccessPatientAsync(Guid u, Guid p, AccessLevel l = AccessLevel.Booking, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DoctorDto?> GetDoctorAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RoomDto?> GetRoomAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? type = null, bool? active = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid id, DateOnly day, string shift, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed record Delivery(string Target, Guid Recipient, string EventName, JsonElement Payload);
    private sealed class Recorder(SharedDbContext db, ConcurrentQueue<Delivery> sends) : IRealtimeNotifier
    {
        private Task Record(string target, Guid id, string eventName, object payload)
        {
            Assert.Null(db.Database.CurrentTransaction);
            sends.Enqueue(new(target, id, eventName, JsonSerializer.SerializeToElement(payload, JsonSerializerOptions.Web))); return Task.CompletedTask;
        }
        public Task ToStaffUserAsync(Guid id, string name, object payload, CancellationToken ct) => Record("user", id, name, payload);
        public Task ToStaffRoomAsync(Guid id, string name, object payload, CancellationToken ct) => Record("room", id, name, payload);
        public Task ToPublicDisplayAsync(Guid id, string name, object payload, CancellationToken ct) => Record("public", id, name, payload);
        public Task ToPatientUserAsync(Guid id, string name, object payload, CancellationToken ct) => Record("patient", id, name, payload);
    }
}
