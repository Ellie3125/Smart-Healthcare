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
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class ClsRoomWorkflowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unpaid_cls_cannot_be_called_and_payment_is_rechecked_before_start()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(QueueResult.PaymentRequired, (await f.Call()).Result);
        Assert.Equal(QueueStatus.Waiting, await f.Status());
        f.Billing.Paid = true;
        Assert.Equal(QueueResult.Called, (await f.Call()).Result);
        f.Billing.Paid = false;
        Assert.Equal(QueueResult.PaymentRequired, (await f.Start()).Result);
        Assert.Equal(QueueStatus.Called, await f.Status());
        Assert.Empty(f.Clinical.Lifecycle); // bị chặn vì chưa trả phí thì không gọi Clinical
        f.Billing.Paid = true;
        Assert.Equal(QueueResult.Called, (await f.Start()).Result);
        Assert.Equal(QueueStatus.InProgress, await f.Status());
        Assert.Equal(nameof(IClinicalModule.StartClsServiceAsync), Assert.Single(f.Clinical.Lifecycle).Split(':')[0]);
    }

    [Fact]
    public async Task Concurrent_paid_calls_allocate_once()
    {
        await using var f = await Fixture.Create(); f.Billing.Paid = true;
        var outcomes = await Task.WhenAll(f.Call(), f.Call());
        Assert.Single(outcomes, x => x.Result == QueueResult.Called);
        Assert.Single(outcomes, x => x.Result == QueueResult.StationBusy);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(1, await db.Set<ActivePatientEngagement>().CountAsync(x => x.CurrentTicketId == f.Ticket, Ct));
        Assert.Equal(1, await db.Set<ActiveStationAllocation>().CountAsync(x => x.CurrentTicketId == f.Ticket, Ct));
    }

    [Fact]
    public async Task Pending_results_include_old_finished_tickets_but_exclude_published_cancelled_and_other_rooms()
    {
        await using var f = await Fixture.Create();
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var valid = Guid.NewGuid(); var published = Guid.NewGuid(); var cancelled = Guid.NewGuid(); var other = Guid.NewGuid();
        foreach (var id in new[] { valid, published, cancelled, other })
            db.Add(new QueueTicket { Id = id, EncounterId = f.Encounter, TargetRoomId = id == other ? Guid.NewGuid() : f.Room,
                ServiceOrderItemId = id, TicketNumber = $"TEST-{id:N}", SequenceNumber = 1, TicketType = TicketType.CLS,
                QueueStatus = QueueStatus.Finished, IssuedAtUtc = DateTime.UtcNow.AddDays(-2), CompletedAtUtc = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync(Ct);
        var order = Guid.NewGuid();
        ServiceOrderItemDto Item(Guid id, string status, string? result) => new(id, order, Guid.NewGuid(), "Test", 100, status, true, DateTime.UtcNow,
            result is null ? null : new CLSResultDto(Guid.NewGuid(), id, "", "", result, null, null, null, null, DateTime.UtcNow));
        f.Clinical.Values = [new(order, f.Encounter, 1, DateTime.UtcNow, Guid.NewGuid(), "Confirmed", true,
            [Item(valid, "ServiceFinished", null), Item(published, "Completed", "Published"), Item(cancelled, "Cancelled", null),
                Item(other, "ServiceFinished", null), Item(f.Item, "ServiceFinished", null)])];
        var page = await scope.ServiceProvider.GetRequiredService<IClsPendingResultReader>().ListAsync(f.Room, 1, 1, Ct);
        Assert.Equal(valid, Assert.Single(page.Items).QueueTicketId); Assert.False(page.HasMore);
        Assert.Empty((await scope.ServiceProvider.GetRequiredService<IClsPendingResultReader>().ListAsync(f.Room, 2, 1, Ct)).Items);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Ticket { get; } = Guid.NewGuid(); public Guid Encounter { get; } = Guid.NewGuid();
        public Guid Item { get; } = Guid.NewGuid(); public Guid Room { get; } = Guid.NewGuid();
        public Guid Station { get; } = Guid.NewGuid(); public Guid Patient { get; } = Guid.NewGuid();
        public ServiceProvider Services { get; private set; } = null!;
        public Fees Billing { get; private set; } = null!;
        public Orders Clinical { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var billing = DispatchProxy.Create<IBillingModule, Fees>();
            f.Billing = (Fees)billing; f.Billing.Encounter = f.Encounter; f.Billing.Item = f.Item;
            var clinical = DispatchProxy.Create<IClinicalModule, Orders>(); f.Clinical = (Orders)clinical;
            var cs = TestDatabase.RequiredConnectionString();
            f.Services = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
                .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton(billing).AddSingleton(clinical).BuildServiceProvider();
            await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.Add(new ServiceStation { Id = f.Station, RoomId = f.Room, StationCode = $"TEST-{f.Station:N}", StationName = "Test", IsActive = true });
            db.Add(new QueueTicket { Id = f.Ticket, EncounterId = f.Encounter, TargetRoomId = f.Room, ServiceOrderItemId = f.Item,
                TicketNumber = $"TEST-{f.Ticket:N}", SequenceNumber = 1, TicketType = TicketType.CLS, QueueStatus = QueueStatus.Waiting, IssuedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(Ct); return f;
        }
        public async Task<QueueOutcome> Call()
        {
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<IQueueStore>()
                .TryAllocateAsync(new(Station, Room, Ticket, Patient, Encounter, Guid.NewGuid()), t), Ct);
        }
        public async Task<QueueOutcome> Start()
        {
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<IQueueStore>()
                .TransitionAsync(new(Ticket, Guid.NewGuid(), Room), TicketTransition.Start, t), Ct);
        }
        public async Task<QueueStatus> Status()
        {
            await using var scope = Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>()
                .Set<QueueTicket>().Where(x => x.Id == Ticket).Select(x => x.QueueStatus).SingleAsync(Ct);
        }
        public async ValueTask DisposeAsync()
        {
            await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var ids = await db.Set<QueueTicket>().Where(x => x.EncounterId == Encounter).Select(x => x.Id).ToListAsync(Ct);
            await db.Set<ActivePatientEngagement>().Where(x => ids.Contains(x.CurrentTicketId)).ExecuteDeleteAsync(Ct);
            await db.Set<ActiveStationAllocation>().Where(x => ids.Contains(x.CurrentTicketId)).ExecuteDeleteAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().Where(x => x.RecordId == Ticket).ExecuteDeleteAsync(Ct);
            await db.Set<QueueTicket>().Where(x => x.EncounterId == Encounter).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceStation>().Where(x => x.Id == Station).ExecuteDeleteAsync(Ct);
            await Services.DisposeAsync();
        }
    }
    public class Orders : DispatchProxy
    {
        public IReadOnlyList<ServiceOrderDto> Values { get; set; } = [];
        public List<string> Lifecycle { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case nameof(IClinicalModule.GetServiceOrdersByEncounterIdAsync): return Task.FromResult(Values);
                // Vé CLS bắt đầu/xong phải báo Clinical trong cùng lệnh.
                case nameof(IClinicalModule.StartClsServiceAsync) or nameof(IClinicalModule.FinishClsServiceAsync):
                    Lifecycle.Add(method.Name + ":" + ((ClsServiceCommand)args![0]!).ServiceOrderItemId);
                    return Task.CompletedTask;
                default: throw new NotSupportedException(method?.Name);
            }
        }
    }
    public class Fees : DispatchProxy
    {
        public Guid Encounter { get; set; } public Guid Item { get; set; } public bool Paid { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IBillingModule.GetEncounterCostSummaryAsync)
            ? Task.FromResult(new EncounterCostSummary(Encounter, "VND", 100, Paid ? 100 : 0, 0, Paid ? 100 : 0, Paid ? 0 : 100, 0,
                [new(Guid.NewGuid(), ChargeType.CLSService, Guid.NewGuid(), "Test", 100, 1, 100, Paid ? 100 : 0, 0, "VND",
                    Paid ? ChargeStatus.Paid : ChargeStatus.Pending, Encounter, null, Item)]))
            : throw new NotSupportedException(method?.Name);
    }
}
