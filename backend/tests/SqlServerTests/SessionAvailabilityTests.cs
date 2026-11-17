using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.Queues;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Coordination;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure.Sessions;
using Xunit;

namespace SqlServerTests;

public sealed partial class SchedulingHoldTests
{
    [Theory]
    [InlineData(Scheduling.Domain.Enums.SessionStatus.Completed)]
    [InlineData(Scheduling.Domain.Enums.SessionStatus.Cancelled)]
    public async Task Opening_the_same_doctor_day_never_reopens_a_terminal_session(Scheduling.Domain.Enums.SessionStatus status)
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET IsOpen=0,SessionStatus='{status}' WHERE Id='{day.Id}'");
            await using var scope = services.CreateAsyncScope();
            var reopened = await scope.ServiceProvider.GetRequiredService<Scheduling.Application.DoctorDays.IDoctorDayStore>().TryOpenAsync(new DoctorDayQuota
            {
                Id = Guid.NewGuid(), DoctorId = day.DoctorId, ServiceDate = day.ServiceDate, AssignedRoomId = day.AssignedRoomId,
                Capacity = day.Capacity, IsOpen = true, SessionStatus = Scheduling.Domain.Enums.SessionStatus.Scheduled,
                SessionStartTimeUtc = day.SessionStartTimeUtc, BookingCutoffTimeUtc = day.BookingCutoffTimeUtc, RescheduleCutoffTimeUtc = day.RescheduleCutoffTimeUtc
            }, Ct);
            Assert.Null(reopened);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.DoctorDayQuota WHERE Id='{day.Id}' AND IsOpen=0 AND SessionStatus='{status}'"));
        }
        finally { await CleanupQueueAsync(day); }
    }

    [Fact]
    public async Task Queue_rechecks_a_station_disabled_after_resolution()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock); var station = Guid.NewGuid();
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, [station]);
            var ticket = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            await using var scope = services.CreateAsyncScope(); var store = scope.ServiceProvider.GetRequiredService<IQueueStore>();
            Assert.Equal(station, (await store.ResolveStationAsync(day.AssignedRoomId, station, "Test", Ct)).StationId);
            await using var connection = new SqlConnection(TestDatabase.RequiredConnectionString()); await connection.OpenAsync(Ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(Ct);
            await using var disable = new SqlCommand("UPDATE scheduling.ServiceStation SET IsActive=0 WHERE Id=@station", connection, transaction);
            disable.Parameters.AddWithValue("@station", station); await disable.ExecuteNonQueryAsync(Ct);
            var pending = Uow(scope).ExecuteAsync(t => store.TryAllocateAsync(new(station, day.AssignedRoomId, ticket.QueueTicketId,
                ticket.PatientId, ticket.EncounterId, Guid.NewGuid()), t), Ct);
            await transaction.CommitAsync(Ct); var outcome = await pending;
            Assert.Equal(QueueResult.StationNotFound, outcome.Result);
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveStationAllocation WHERE StationId='{station}'"));
        }
        finally { await CleanupQueueAsync(day); }
    }

    [Fact]
    public async Task Session_waits_for_clinical_conclusion_and_all_tickets_final_then_closes_once_in_parallel()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET SessionStartTimeUtc='{AtIct(6,30):yyyy-MM-ddTHH:mm:ss}' WHERE Id='{day.Id}'");
            var appointment = await BookedAsync(services, day); var ticket = await CheckInAsync(services, appointment.AppointmentId);
            clock.NowUtc = AtIct(14, 0);
            await ExecAsync($"UPDATE scheduling.QueueTicket SET QueueStatus='Finished' WHERE Id='{ticket.QueueTicketId}'; UPDATE clinical.Encounter SET Status='WaitingCLS' WHERE Id='{ticket.EncounterId}'");
            async Task<bool> Complete()
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<SessionCompletionJob>().TryCompleteAsync(day.Id, Ct);
            }
            Assert.False(await Complete());
            await ExecAsync($"UPDATE clinical.Encounter SET Status='Abandoned' WHERE Id='{ticket.EncounterId}'");
            Assert.False(await Complete());
            var back = Guid.NewGuid();
            await ExecAsync($"""
                UPDATE clinical.Encounter SET Status='Completed' WHERE Id='{ticket.EncounterId}';
                INSERT INTO scheduling.QueueTicket (Id,EncounterId,TargetRoomId,ServiceOrderId,TicketNumber,TicketType,SequenceNumber,QueueStatus,IssuedAtUtc)
                VALUES ('{back}','{ticket.EncounterId}','{day.AssignedRoomId}',NEWID(),'TEST-{back:N}','ReturnConsultation',1,'Deferred',SYSUTCDATETIME());
                """);
            Assert.False(await Complete());
            await ExecAsync($"UPDATE scheduling.QueueTicket SET QueueStatus='Cancelled' WHERE Id='{back}'");
            var results = await Task.WhenAll(Complete(), Complete()); Assert.Single(results, x => x);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.Appointment WHERE Id='{appointment.AppointmentId}' AND Status='Completed'"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.DoctorDayQuota WHERE Id='{day.Id}' AND SessionStatus='Completed' AND IsOpen=0 AND AdmittedCount=1"));
            Assert.Equal(0, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveBookingClaim WHERE AppointmentId='{appointment.AppointmentId}' AND IsActive=1"));
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM shared.AuditLog WHERE RecordId='{day.Id}' AND ChangedColumns='SessionStatus,IsOpen'"));
        }
        finally { await CleanupQueueAsync(day); await ExecAsync($"DELETE FROM shared.AuditLog WHERE RecordId='{day.Id}'"); }
    }

    [Fact]
    public async Task Session_does_not_close_empty_or_scheduled_but_can_close_when_last_appointment_is_absent()
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock);
        try
        {
            await ExecAsync($"UPDATE scheduling.DoctorDayQuota SET SessionStartTimeUtc='{AtIct(6,30):yyyy-MM-ddTHH:mm:ss}' WHERE Id='{day.Id}'");
            async Task<bool> Complete()
            { await using var scope = services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<SessionCompletionJob>().TryCompleteAsync(day.Id, Ct); }
            Assert.False(await Complete()); var appointment = await BookedAsync(services, day); Assert.False(await Complete());
            var patient = Guid.NewGuid();
            var held = await HoldAsync(services, day, await SeedAttemptAsync(services, patient), patient);
            await ExecAsync($"UPDATE scheduling.Appointment SET Status='Absent' WHERE Id='{appointment.AppointmentId}'");
            clock.NowUtc = AtIct(11, 0);
            Assert.False(await Complete());
            await using (var scope = services.CreateAsyncScope())
                Assert.True(await Uow(scope).ExecuteAsync(t => scope.ServiceProvider.GetRequiredService<Scheduling.Application.Holds.IHoldStore>().ExpireAsync(held.ReservationId!.Value, t), Ct));
            Assert.True(await Complete());
        }
        finally { await CleanupQueueAsync(day); await ExecAsync($"DELETE FROM shared.AuditLog WHERE RecordId='{day.Id}'"); }
    }

    [Theory]
    [InlineData("TargetRoomInactive")]
    [InlineData("NoActiveStation")]
    [InlineData("OriginalDoctorInactive")]
    public async Task Coordination_snapshot_reports_unavailability_and_preserves_in_progress_allocation(string reason)
    {
        var clock = new BoundaryDbClock(AtIct(8, 0)); await using var services = QueueServices(clock);
        var day = await SeedBoundaryDayAsync(services, clock); var station = Guid.NewGuid();
        try
        {
            await SeedStationsAsync(day.AssignedRoomId, [station]);
            var ticket = await CheckInAsync(services, (await BookedAsync(services, day)).AppointmentId);
            await CallNextAsync(services, day, station); await TransitionAsync(services, ticket.QueueTicketId, day.AssignedRoomId, TicketTransition.Start);
            if (reason == "NoActiveStation") await ExecAsync($"UPDATE scheduling.ServiceStation SET IsActive=0 WHERE Id='{station}'");
            await using var scope = services.CreateAsyncScope();
            var identity = System.Reflection.DispatchProxy.Create<IIdentityPatientsModule, AvailabilityIdentity>();
            ((AvailabilityIdentity)identity).RoomActive = reason != "TargetRoomInactive";
            ((AvailabilityIdentity)identity).DoctorActive = reason != "OriginalDoctorInactive";
            var reader = new CoordinationExceptionReader(scope.ServiceProvider.GetRequiredService<SchedulingDbContext>(),
                scope.ServiceProvider.GetRequiredService<IClinicalModule>(), identity);
            var item = Assert.Single(await reader.ListAsync(100, Ct), x => x.QueueTicketId == ticket.QueueTicketId);
            Assert.Equal(reason, item.Reason); Assert.True(item.RequiresManualHandling);
            Assert.Equal(1, await CountAsync($"SELECT COUNT(*) FROM scheduling.ActiveStationAllocation WHERE CurrentTicketId='{ticket.QueueTicketId}'"));
        }
        finally { await CleanupQueueAsync(day); }
    }
    public class AvailabilityIdentity : QueueIdentity
    {
        public bool RoomActive { get; set; } = true;
        public bool DoctorActive { get; set; } = true;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IIdentityPatientsModule.GetRoomAsync) => Task.FromResult<RoomDto?>(new((Guid)args![0]!, "TEST", "Test", "Clinical", RoomActive)),
            nameof(IIdentityPatientsModule.GetDoctorAsync) => Task.FromResult<DoctorDto?>(new((Guid)args![0]!, "Test", Guid.NewGuid(), "Test", Guid.NewGuid(), "Test", DoctorActive)),
            _ => base.Invoke(method, args)
        };
    }
}
