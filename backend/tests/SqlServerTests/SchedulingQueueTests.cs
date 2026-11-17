using System.Reflection;
using Clinical.Infrastructure;
using Clinical.Infrastructure.Persistence;
using IdentityPatients.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Application;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Xunit;

namespace SqlServerTests;

// W3-03 lõi hàng đợi, W3-04 vé CLS + vé quay lại, trên SQL Server thật.
public sealed partial class SchedulingHoldTests
{
    [Fact]
    public async Task Queue_two_stations_calling_together_take_distinct_tickets_and_double_click_returns_the_same_ticket()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var stations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, stations);
            foreach (var booked in new[] { await BookedAsync(services, day), await BookedAsync(services, day) })
                await CheckInAsync(services, booked.AppointmentId);

            var results = await Task.WhenAll(CallNextAsync(services, day, stations[0]), CallNextAsync(services, day, stations[1]),
                CallNextAsync(services, day, stations[0]));

            Assert.All(results, r => Assert.Equal(QueueResult.Called, r.Result));
            Assert.Equal(results[0].Ticket!.QueueTicketId, results[2].Ticket!.QueueTicketId);
            Assert.Single(results, r => r.AlreadyCalled);
            Assert.NotEqual(results[0].Ticket!.QueueTicketId, results[1].Ticket!.QueueTicketId);
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveStationAllocation WHERE StationId IN ('{stations[0]}', '{stations[1]}')"));
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE TargetRoomId = '{day.AssignedRoomId}' AND QueueStatus = 'Called'"));
        }
        finally { await CleanupQueueAsync(day); }
    }

    [Fact]
    public async Task Queue_single_waiting_ticket_goes_to_exactly_one_of_two_stations()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var stations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, stations);
            await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);

            var results = await Task.WhenAll(CallNextAsync(services, day, stations[0]), CallNextAsync(services, day, stations[1]));

            Assert.Single(results, r => r.Result == QueueResult.Called);
            Assert.Single(results, r => r.Result == QueueResult.QueueEmpty);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActivePatientEngagement WHERE CurrentStationId IN ('{stations[0]}', '{stations[1]}')"));
        }
        finally { await CleanupQueueAsync(day); }
    }

    [Fact]
    public async Task Queue_skips_patient_busy_at_another_station_keeps_number_and_calls_after_release()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var otherRoom = Guid.NewGuid();
        try
        {
            var first = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            var second = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            // Bệnh nhân đầu đang được gọi ở phòng CLS khác.
            var cls = Guid.CreateVersion7();
            await ExecAsync($"""
                INSERT INTO scheduling.QueueTicket (Id, EncounterId, TargetRoomId, TicketNumber, TicketType, SequenceNumber, QueueStatus, IssuedAtUtc)
                VALUES ('{cls}', '{first.EncounterId}', '{otherRoom}', 'TEST-{cls:N}', 'CLS', 1, 'Waiting', '{AtIct(7, 59):yyyy-MM-ddTHH:mm:ss}')
                """);
            var atCls = await CallNextAsync(services, otherRoom, null);
            Assert.Equal(cls, atCls.Ticket!.QueueTicketId);

            var called = await CallNextAsync(services, day, null);
            Assert.Equal(second.QueueTicketId, called.Ticket!.QueueTicketId);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE Id = '{first.QueueTicketId}' AND QueueStatus = 'Waiting' AND SequenceNumber = 1"));

            // Phòng CLS khám xong → bệnh nhân rảnh; phòng khám xong lượt hiện tại rồi gọi tiếp ra đúng vé số 1.
            foreach (var (ticket, room) in new[] { (cls, otherRoom), (second.QueueTicketId, day.AssignedRoomId) })
            {
                Assert.Equal(QueueResult.Called, (await TransitionAsync(services, ticket, room, TicketTransition.Start)).Result);
                Assert.Equal(QueueResult.Called, (await TransitionAsync(services, ticket, room, TicketTransition.Finish)).Result);
            }
            Assert.Equal(QueueResult.WrongRoom, (await TransitionAsync(services, first.QueueTicketId, otherRoom, TicketTransition.Start)).Result);
            Assert.Equal(first.QueueTicketId, (await CallNextAsync(services, day, null)).Ticket!.QueueTicketId);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActivePatientEngagement WHERE CurrentTicketId IN ('{cls}', '{second.QueueTicketId}')"));
        }
        finally { await CleanupQueueAsync(day, otherRoom); }
    }

    [Fact]
    public async Task Cls_tickets_issue_waiting_cls_and_deferred_return_replay_and_roll_back_with_the_caller()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0));
        await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        var clsRoom = Guid.NewGuid();
        var service = Guid.NewGuid();
        try
        {
            await ExecAsync($"""
                INSERT INTO scheduling.MedicalService (Id, ServiceCode, ServiceName, ServiceCategory, DefaultRoomId, IsMandatoryDefault, IsActive)
                VALUES ('{service}', 'T-{service:N}', N'Xét nghiệm thử', 'Laboratory', '{clsRoom}', 1, 1);
                INSERT INTO scheduling.MedicalServiceRoom (Id, MedicalServiceId, RoomId, IsDefaultRoom, IsActive)
                VALUES (NEWID(), '{service}', '{clsRoom}', 1, 1);
                """);
            var initial = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            var order = new CreateClsTickets(initial.EncounterId, Guid.NewGuid(),
                [new(Guid.NewGuid(), service), new(Guid.NewGuid(), service)], null);

            var created = await CreateClsAsync(services, order);
            var replay = await CreateClsAsync(services, order);

            Assert.Equal(new[] { 1, 2 }, created.ClsTickets.Select(x => x.SequenceNumber));
            Assert.All(created.ClsTickets, t => Assert.Equal(clsRoom, t.TargetRoomId));
            Assert.Equal(day.AssignedRoomId, created.ReturnTicket.TargetRoomId);
            Assert.Equal(created.ReturnTicket, replay.ReturnTicket);
            Assert.Equal(created.ClsTickets, replay.ClsTickets);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE Id = '{created.ReturnTicket.QueueTicketId}' AND QueueStatus = 'Deferred'"));
            Assert.Equal(2, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE ServiceOrderItemId IS NOT NULL AND EncounterId = '{initial.EncounterId}' AND QueueStatus = 'Waiting'"));

            // Bên gọi (Clinical) lỗi sau khi tạo vé: vé của đợt đó rollback cùng transaction.
            var failed = order with { ServiceOrderId = Guid.NewGuid(), Lines = [new(Guid.NewGuid(), service)] };
            await using (var scope = services.CreateAsyncScope())
                await Assert.ThrowsAsync<RollbackSignal>(() => Uow(scope).ExecuteAsync(async t =>
                {
                    await scope.ServiceProvider.GetRequiredService<ISchedulingModule>().CreateClsTicketsAsync(failed, t);
                    throw new RollbackSignal();
                }, Ct));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.QueueTicket WHERE ServiceOrderId = '{failed.ServiceOrderId}' OR ServiceOrderItemId = '{failed.Lines[0].ServiceOrderItemId}'"));

            var unknown = order with { ServiceOrderId = Guid.NewGuid(), Lines = [new(Guid.NewGuid(), Guid.NewGuid())] };
            var error = await Assert.ThrowsAsync<SchedulingException>(() => CreateClsAsync(services, unknown));
            Assert.Equal(SchedulingErrorCode.MedicalServiceUnavailable, error.Code);
        }
        finally
        {
            await CleanupQueueAsync(day, clsRoom);
            await ExecAsync($"DELETE FROM scheduling.MedicalServiceRoom WHERE MedicalServiceId = '{service}'; DELETE FROM scheduling.MedicalService WHERE Id = '{service}';");
        }
    }

    private static async Task<ClsTicketsCreated> CreateClsAsync(ServiceProvider services, CreateClsTickets order)
    {
        await using var scope = services.CreateAsyncScope();
        return await Uow(scope).ExecuteAsync(t =>
            scope.ServiceProvider.GetRequiredService<ISchedulingModule>().CreateClsTicketsAsync(order, t), Ct);
    }

    private static Task<QueueOutcome> CallNextAsync(ServiceProvider services, DoctorDayQuota day, Guid? station) =>
        CallNextAsync(services, day.AssignedRoomId, station);

    private static async Task<QueueOutcome> CallNextAsync(ServiceProvider services, Guid roomId, Guid? station)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<QueueService>().CallNextAsync(new(roomId, station, Guid.NewGuid()), Ct);
    }

    private static async Task<QueueOutcome> TransitionAsync(ServiceProvider services, Guid ticket, Guid room, TicketTransition transition)
    {
        await using var scope = services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<QueueService>();
        var command = new TicketCommand(ticket, Guid.NewGuid(), room);
        return transition == TicketTransition.Start ? await queue.StartAsync(command, Ct) : await queue.FinishAsync(command, Ct);
    }

    private static Task SeedStationsAsync(Guid roomId, Guid[] stations) => ExecAsync(string.Join(";", stations.Select(s => $"""
        INSERT INTO scheduling.ServiceStation (Id, RoomId, StationCode, StationName, IsActive)
        VALUES ('{s}', '{roomId}', 'TEST-{s:N}', N'Trạm thử', 1)
        """)));

    private static async Task CleanupQueueAsync(DoctorDayQuota day, params Guid[] otherRooms)
    {
        var rooms = string.Join(", ", otherRooms.Append(day.AssignedRoomId).Select(r => $"'{r}'"));
        await ExecAsync($"""
            DECLARE @tickets TABLE (Id uniqueidentifier);
            INSERT INTO @tickets SELECT Id FROM scheduling.QueueTicket WHERE TargetRoomId IN ({rooms});
            DELETE FROM scheduling.ActivePatientEngagement WHERE CurrentTicketId IN (SELECT Id FROM @tickets);
            DELETE FROM scheduling.ActiveStationAllocation WHERE CurrentTicketId IN (SELECT Id FROM @tickets);
            DELETE FROM shared.AuditLog WHERE RecordId IN (SELECT Id FROM @tickets);
            DELETE FROM scheduling.QueueTicket WHERE Id IN (SELECT Id FROM @tickets) AND TicketType <> 'InitialConsultation';
            """);
        await CleanupAttendanceAsync(day);
        await ExecAsync($"DELETE FROM scheduling.ServiceStation WHERE RoomId IN ({rooms})");
    }

    private static ServiceProvider QueueServices(BoundaryDbClock clock)
    {
        var cs = TestDatabase.RequiredConnectionString();
        return new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddNotificationsInfrastructure(cs).AddClinicalInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddUnitOfWorkParticipant<WorkflowDbContext>()
            .AddUnitOfWorkParticipant<ClinicalDbContext>().AddSchedulingApplication()
            .AddScoped<Scheduling.Application.DoctorSearch.IDoctorSearchDirectory, Scheduling.Application.DoctorSearch.IdentityDoctorSearchDirectory>()
            .AddSingleton<Billing.Contracts.IBillingModule>(new FixedFeeBilling())
            .AddSingleton<Billing.Contracts.IPaymentRefunds>(new RecordingRefunds())
            .AddSingleton<IDbClock>(clock)
            .AddSingleton(DispatchProxy.Create<IIdentityPatientsModule, QueueIdentity>())
            .BuildServiceProvider(validateScopes: true);
    }

    // Bác sĩ và phòng giả: mọi phòng đều tồn tại, đang hoạt động.
    public class QueueIdentity : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "GetDoctorAsync" => Task.FromResult<DoctorDto?>(new((Guid)args![0]!, "BS Test", Guid.NewGuid(), "TEST", Guid.NewGuid(), "BS", true)),
            "GetRoomAsync" => Task.FromResult<RoomDto?>(new((Guid)args![0]!, "PT", "Phòng thử", "Clinical", true)),
            _ => throw new NotSupportedException(method?.Name),
        };
    }
}
