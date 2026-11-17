using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public sealed class ClinicalClsResultsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unconfigured_ticket_validation_denies_instead_of_inventing_patient_and_result_ids()
    {
        var module = new Scheduling.Application.SchedulingModule(null!, null!);
        Assert.Null(await module.ValidateClsTicketForRoomAsync(Guid.NewGuid(), Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Scheduling_lifecycle_writes_real_item_and_draft_result_and_replays_without_duplication()
    {
        await using var f = await ClsResultFixture.CreateAsync();
        await f.TransitionAsync(true);
        Assert.Equal(ItemStatus.InProgress, (await f.ItemAsync()).ItemStatus);
        await f.TransitionAsync(true);
        await f.TransitionAsync(false);
        await f.TransitionAsync(false);
        Assert.Equal(ItemStatus.ServiceFinished, (await f.ItemAsync()).ItemStatus);
        await using var scope = f.Services.CreateAsyncScope();
        var results = await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<CLSResult>().Where(x => x.ServiceOrderItemId == f.ItemId).ToListAsync(Ct);
        Assert.Equal(ResultStatus.Draft, Assert.Single(results).ResultStatus);
        Assert.NotNull((await f.ItemAsync()).CompletedAtUtc);
        var module = new Scheduling.Application.SchedulingModule(null!, null!, clsTicketReader: () => scope.ServiceProvider.GetRequiredService<Scheduling.Contracts.IClsTicketReader>(),
            clinical: () => scope.ServiceProvider.GetRequiredService<IClinicalModule>());
        var context = await module.ValidateClsTicketForRoomAsync(f.TicketId, f.RoomId, Ct);
        Assert.NotNull(context);
        Assert.Equal(f.EncounterId, context.EncounterId);
        Assert.Equal(results[0].Id, context.CLSResultId);
        Assert.True(context.IsServiceFinished);
        Assert.False(context.IsResultReturned);
        Assert.Null(await module.ValidateClsTicketForRoomAsync(f.TicketId, Guid.NewGuid(), Ct));
        Assert.Null(await module.ValidateClsTicketForRoomAsync(Guid.NewGuid(), f.RoomId, Ct));
    }

    [Fact]
    public async Task Caller_failure_rolls_back_the_scheduling_transition_and_clinical_writer()
    {
        await using var f = await ClsResultFixture.CreateAsync();
        await Assert.ThrowsAsync<Rollback>(() => f.TransitionAsync(true, fail: true));
        Assert.Equal(ItemStatus.Ordered, (await f.ItemAsync()).ItemStatus);
        await using var scope = f.Services.CreateAsyncScope();
        Assert.Equal(QueueStatus.Called, (await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().SingleAsync(x => x.Id == f.TicketId, Ct)).QueueStatus);
        Assert.False(await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().AnyAsync(x => x.PerformedByUserId == f.UserId, Ct));
    }

    [Fact]
    public async Task Lifecycle_refuses_missing_transaction_wrong_room_and_finish_before_start()
    {
        await using var f = await ClsResultFixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var clinical = scope.ServiceProvider.GetRequiredService<IClinicalModule>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => clinical.StartClsServiceAsync(new(f.ItemId, f.RoomId, f.UserId), Ct));
        await Assert.ThrowsAsync<ClinicalException>(() => f.TransitionAsync(false));
        await Assert.ThrowsAsync<ClinicalException>(() => f.TransitionAsync(true, room: Guid.NewGuid()));
        Assert.Equal(ItemStatus.Ordered, (await f.ItemAsync()).ItemStatus);
    }

    [Fact]
    public async Task Invalid_missing_closed_and_mismatched_contexts_never_advance_the_item()
    {
        await using var f = await ClsResultFixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var clinical = scope.ServiceProvider.GetRequiredService<IClinicalModule>();
        Assert.Equal(ClinicalErrorCode.InvalidRequest, (await Assert.ThrowsAsync<ClinicalException>(() =>
            clinical.StartClsServiceAsync(new(Guid.Empty, f.RoomId, f.UserId), Ct))).Code);
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.Equal(ClinicalErrorCode.OrderItemNotFound, (await Assert.ThrowsAsync<ClinicalException>(() => uow.ExecuteAsync(t =>
            clinical.StartClsServiceAsync(new(Guid.NewGuid(), f.RoomId, f.UserId), t), Ct))).Code);
        Assert.Equal(ClinicalErrorCode.InvalidStatusTransition, (await Assert.ThrowsAsync<ClinicalException>(() => uow.ExecuteAsync(t =>
            clinical.StartClsServiceAsync(new(f.ItemId, f.RoomId, f.UserId), t), Ct))).Code);
        await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().Where(x => x.Id == f.EncounterId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EncounterStatus.Completed), Ct);
        Assert.Equal(ClinicalErrorCode.EncounterClosed, (await Assert.ThrowsAsync<ClinicalException>(() => f.TransitionAsync(true))).Code);
        await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<Encounter>().Where(x => x.Id == f.EncounterId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EncounterStatus.WaitingCLS), Ct);
        await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().Where(x => x.Id == f.TicketId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EncounterId, Guid.NewGuid()), Ct);
        Assert.Equal(ClinicalErrorCode.Forbidden, (await Assert.ThrowsAsync<ClinicalException>(() => f.TransitionAsync(true))).Code);
        Assert.Equal(ItemStatus.Ordered, (await f.ItemAsync()).ItemStatus);
    }

    internal sealed class Rollback : Exception;
}

internal sealed class ClsResultFixture(ServiceProvider services) : IAsyncDisposable
{
    public ServiceProvider Services { get; } = services;
    public Guid EncounterId { get; } = Guid.NewGuid();
    public Guid OrderId { get; } = Guid.NewGuid();
    public Guid ItemId { get; } = Guid.NewGuid();
    public Guid TicketId { get; } = Guid.NewGuid();
    public Guid RoomId { get; } = Guid.NewGuid();
    public Guid UserId { get; } = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public static async Task<ClsResultFixture> CreateAsync()
    {
        var cs = TestDatabase.RequiredConnectionString();
        var services = new ServiceCollection().AddSharedInfrastructure(cs).AddClinicalInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddUnitOfWorkParticipant<ClinicalDbContext>().AddUnitOfWorkParticipant<SchedulingDbContext>().BuildServiceProvider();
        var f = new ClsResultFixture(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        db.Add(new Encounter { Id = f.EncounterId, AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(), OriginalDoctorId = Guid.NewGuid(),
            OriginalRoomId = f.RoomId, SpecialtySnapshot = "Test", DoctorRankSnapshot = "Test", CheckedInAtUtc = DateTime.UtcNow, Status = EncounterStatus.WaitingCLS });
        db.Add(new ServiceOrder { Id = f.OrderId, EncounterId = f.EncounterId, OrderRoundNumber = 1, OrderedAtUtc = DateTime.UtcNow,
            PrescribedByDoctorId = Guid.NewGuid(), OrderBatchStatus = OrderBatchStatus.Confirmed });
        db.Add(new ServiceOrderItem { Id = f.ItemId, ServiceOrderId = f.OrderId, MedicalServiceId = Guid.NewGuid(), ServiceNameSnapshot = "Test",
            AppliedPrice = 1, ItemStatus = ItemStatus.Ordered, IsMandatory = true });
        await db.SaveChangesAsync(Ct);
        var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        scheduling.Add(new QueueTicket { Id = f.TicketId, EncounterId = f.EncounterId, ServiceOrderItemId = f.ItemId, TargetRoomId = f.RoomId,
            TicketNumber = f.TicketId.ToString("N"), TicketType = TicketType.CLS, SequenceNumber = 1, QueueStatus = QueueStatus.Called, IssuedAtUtc = DateTime.UtcNow });
        await scheduling.SaveChangesAsync(Ct);
        return f;
    }
    public async Task TransitionAsync(bool start, bool fail = false, Guid? room = null)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(async t =>
        {
            await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().Where(x => x.Id == TicketId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QueueStatus, start ? QueueStatus.InProgress : QueueStatus.Finished), t);
            var clinical = scope.ServiceProvider.GetRequiredService<IClinicalModule>();
            var command = new ClsServiceCommand(ItemId, room ?? RoomId, UserId);
            if (start) await clinical.StartClsServiceAsync(command, t); else await clinical.FinishClsServiceAsync(command, t);
            if (fail) throw new ClinicalClsResultsTests.Rollback();
        }, Ct);
    }
    public async Task<ServiceOrderItem> ItemAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ClinicalDbContext>().Set<ServiceOrderItem>().AsNoTracking().SingleAsync(x => x.Id == ItemId, Ct);
    }
    public async ValueTask DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
            var resultIds = db.Set<CLSResult>().Where(x => x.ServiceOrderItemId == ItemId).Select(x => x.Id);
            await db.Set<CLSResultRevision>().Where(x => resultIds.Contains(x.CLSResultId)).ExecuteDeleteAsync(Ct);
            await db.Set<CLSResult>().Where(x => x.ServiceOrderItemId == ItemId).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceOrderItem>().Where(x => x.Id == ItemId).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceOrder>().Where(x => x.Id == OrderId).ExecuteDeleteAsync(Ct);
            await db.Set<Encounter>().Where(x => x.Id == EncounterId).ExecuteDeleteAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<QueueTicket>().Where(x => x.Id == TicketId).ExecuteDeleteAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().Where(x => x.PerformedByUserId == UserId).ExecuteDeleteAsync(Ct);
        }
        await Services.DisposeAsync();
    }
}
