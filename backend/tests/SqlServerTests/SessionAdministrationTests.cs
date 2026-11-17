using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.SessionAdministration;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed partial class SessionAdministrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Replacement_preserves_counters_appointment_and_fee_and_updates_all_active_claims()
    {
        await using var f = await Fixture.CreateAsync();
        await f.ReplaceAsync(f.Directory.Replacement);
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var quota = await db.Set<DoctorDayQuota>().SingleAsync(x => x.Id == f.QuotaId, Ct);
        Assert.Equal(f.Directory.Replacement, quota.DoctorId);
        Assert.Equal((1, 1, 2), (quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount));
        Assert.All(await db.Set<ActiveBookingClaim>().Where(x => x.SessionQuotaId == f.QuotaId && x.IsActive).ToListAsync(Ct), x => Assert.Equal(quota.DoctorId, x.DoctorId));
        var appointment = await db.Set<Appointment>().SingleAsync(x => x.Id == f.AppointmentId, Ct);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(150_000m, appointment.AppliedConsultationFee);
        Assert.Equal(f.Directory.Original, (await db.Set<BookingAttempt>().SingleAsync(x => x.Id == f.HeldAttemptId, Ct)).SelectedDoctorId);
        var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
        Assert.Equal(1, await shared.Set<OutboxMessage>().CountAsync(x => x.AggregateId == f.AppointmentId && x.EventType.ToString() == "AppointmentDoctorChanged", Ct));
        Assert.Equal(1, await shared.Set<AuditLog>().CountAsync(x => x.RecordId == f.QuotaId, Ct));
        var calls = await scope.ServiceProvider.GetRequiredService<ISessionAdministration>().GetCallListAsync(1, 50, Ct);
        Assert.Contains(calls.Items, x => x.AppointmentId == f.AppointmentId && x.NewDoctorId == f.Directory.Replacement);
        Assert.True(calls.ContactsAvailable);
        Assert.Equal("Test patient", Assert.Single(calls.Items, x => x.AppointmentId == f.AppointmentId).PatientName);
        Assert.Equal("*******567", Assert.Single(calls.Items, x => x.AppointmentId == f.AppointmentId).MaskedPhoneNumber);
        Assert.Equal(1, f.Directory.ContactCalls);
        f.Directory.OmitContacts = true;
        var missingContacts = await scope.ServiceProvider.GetRequiredService<ISessionAdministration>().GetCallListAsync(1, 50, Ct);
        Assert.True(missingContacts.ContactsAvailable);
        Assert.All(missingContacts.Items, x => { Assert.Null(x.PatientName); Assert.Null(x.MaskedPhoneNumber); });
    }

    [Fact]
    public async Task Replacement_rejects_an_existing_target_session_and_does_not_change_the_source()
    {
        await using var f = await Fixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var other = f.Day(f.Directory.Replacement);
        db.Add(other); await db.SaveChangesAsync(Ct);
        f.ExtraQuotaIds.Add(other.Id);
        var error = await Assert.ThrowsAsync<SessionAdminException>(() => f.ReplaceAsync(f.Directory.Replacement));
        Assert.Equal(SessionAdminError.AlreadyHasSession, error.Error);
        Assert.Equal(f.Directory.Original, (await db.Set<DoctorDayQuota>().AsNoTracking().SingleAsync(x => x.Id == f.QuotaId, Ct)).DoctorId);
    }

    [Fact]
    public async Task Two_sources_cannot_both_assign_the_same_replacement_on_the_same_day()
    {
        await using var a = await Fixture.CreateAsync();
        await using var b = await Fixture.CreateAsync();
        b.Directory.Replacement = a.Directory.Replacement;
        async Task<bool> Replace(Fixture f)
        {
            try { await f.ReplaceAsync(f.Directory.Replacement); return true; }
            catch (SessionAdminException e) when (e.Error == SessionAdminError.AlreadyHasSession) { return false; }
        }
        var results = await Task.WhenAll(Replace(a), Replace(b));
        Assert.Single(results, x => x);
        Assert.Single(results, x => !x);
    }

    [Fact]
    public async Task Cancelling_before_start_releases_holds_and_bookings_and_emits_full_hospital_refund_once()
    {
        await using var f = await Fixture.CreateAsync();
        await f.CancelAsync(); f.Clock.Now = f.Clock.Now.AddHours(2); await f.CancelAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var quota = await db.Set<DoctorDayQuota>().SingleAsync(x => x.Id == f.QuotaId, Ct);
        Assert.False(quota.IsOpen); Assert.Equal(SessionStatus.Cancelled, quota.SessionStatus);
        Assert.Equal((0, 0, 2), (quota.HeldCount, quota.AdmittedCount, quota.LockedCancelledCount));
        Assert.Equal(AppointmentStatus.Cancelled, (await db.Set<Appointment>().SingleAsync(x => x.Id == f.AppointmentId, Ct)).Status);
        Assert.False(await db.Set<ActiveBookingClaim>().AnyAsync(x => x.SessionQuotaId == f.QuotaId && x.IsActive, Ct));
        var messages = await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<OutboxMessage>()
            .Where(x => x.AggregateId == f.AppointmentId && x.EventType == EventType.AppointmentCancelled).ToListAsync(Ct);
        var json = Assert.Single(messages).PayloadJson;
        Assert.Contains("\"CancelledBy\":\"Hospital\"", json); Assert.Contains("\"Refund\":\"Full\"", json);
        Assert.Contains("\"SlotReleased\":true", json);
        var store = scope.ServiceProvider.GetRequiredService<Scheduling.Application.DoctorDays.IDoctorDayStore>();
        Assert.Null(await store.TryOpenAsync(f.Day(f.Directory.Original), Ct));
        var reservationId = await db.Set<Reservation>().Where(x => x.BookingAttemptId == f.HeldAttemptId).Select(x => x.Id).SingleAsync(Ct);
        var confirmation = scope.ServiceProvider.GetRequiredService<Scheduling.Application.Confirmations.IConfirmationStore>();
        var late = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(t => confirmation.ConfirmAsync(reservationId, 150_000m, t), Ct);
        Assert.IsType<Scheduling.Application.Confirmations.ConfirmResult.PaidUnallocated>(late);
    }

    [Fact]
    public async Task Started_or_checked_in_sessions_and_invalid_requests_are_rejected()
    {
        await using var f = await Fixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var admin = scope.ServiceProvider.GetRequiredService<ISessionAdministration>();
        Assert.Equal(SessionAdminError.InvalidInput, (await Assert.ThrowsAsync<SessionAdminException>(() => admin.ReplaceDoctorAsync(f.Directory.Original, Fixture.Date, f.Directory.Replacement, Guid.NewGuid(), "", Ct))).Error);
        Assert.Equal(SessionAdminError.DoctorUnavailable, (await Assert.ThrowsAsync<SessionAdminException>(() => f.ReplaceAsync(Guid.NewGuid()))).Error);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        await db.Set<Appointment>().Where(x => x.Id == f.AppointmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.CheckedIn), Ct);
        Assert.Equal(SessionAdminError.AlreadyCheckedIn, (await Assert.ThrowsAsync<SessionAdminException>(() => f.ReplaceAsync(f.Directory.Replacement))).Error);
        await db.Set<Appointment>().Where(x => x.Id == f.AppointmentId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.Scheduled), Ct);
        await db.Set<DoctorDayQuota>().Where(x => x.Id == f.QuotaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldCount, 5), Ct);
        Assert.Equal(SessionAdminError.InvalidState, (await Assert.ThrowsAsync<SessionAdminException>(() => f.CancelAsync())).Error);
        await db.Set<DoctorDayQuota>().Where(x => x.Id == f.QuotaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldCount, 1).SetProperty(x => x.IsOpen, false), Ct);
        Assert.Equal(SessionAdminError.InvalidState, (await Assert.ThrowsAsync<SessionAdminException>(() => f.ReplaceAsync(f.Directory.Replacement))).Error);
        await db.Set<DoctorDayQuota>().Where(x => x.Id == f.QuotaId).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsOpen, true), Ct);
        Assert.Equal(SessionAdminError.DayNotFound, (await Assert.ThrowsAsync<SessionAdminException>(() => admin.CancelSessionAsync(Guid.NewGuid(), Fixture.Date, Guid.NewGuid(), "Test", Ct))).Error);
        f.Clock.Now = f.Clock.Now.AddMinutes(90);
        Assert.Equal(SessionAdminError.SessionStarted, (await Assert.ThrowsAsync<SessionAdminException>(() => f.CancelAsync())).Error);
    }

    [Fact]
    public async Task Disabling_room_preserves_active_tickets_and_allocations_and_audits_each_changed_record()
    {
        await using var f = await Fixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var station = new ServiceStation { Id = Guid.NewGuid(), RoomId = f.RoomId, StationCode = "TEST-" + Guid.NewGuid().ToString("N"), StationName = "Bàn giả", IsActive = true };
        var service = new MedicalService { Id = Guid.NewGuid(), DefaultRoomId = f.RoomId, ServiceCode = "TEST-" + Guid.NewGuid().ToString("N"), ServiceName = "Dịch vụ giả", ServiceCategory = ServiceCategory.Laboratory, IsActive = true };
        var mapping = new MedicalServiceRoom { Id = Guid.NewGuid(), MedicalServiceId = service.Id, RoomId = f.RoomId, IsDefaultRoom = true, IsActive = true };
        var ticket = new QueueTicket { Id = Guid.NewGuid(), EncounterId = f.EncounterId, TargetRoomId = f.RoomId, TargetStationId = station.Id,
            TicketNumber = Guid.NewGuid().ToString("N"), SequenceNumber = 1, TicketType = TicketType.CLS, QueueStatus = QueueStatus.InProgress, IssuedAtUtc = f.Clock.Now };
        db.AddRange(station, service, mapping, ticket);
        db.Add(new ActiveStationAllocation { StationId = station.Id, CurrentTicketId = ticket.Id, CurrentPatientId = Guid.NewGuid(), AllocatedAtUtc = f.Clock.Now });
        await db.SaveChangesAsync(Ct);
        var admin = scope.ServiceProvider.GetRequiredService<ISessionAdministration>();
        Assert.Equal(new RoomDisabledView(f.RoomId, 1, 1), await admin.DisableRoomAsync(f.RoomId, Guid.NewGuid(), "Thiết bị hỏng", Ct));
        Assert.Equal(new RoomDisabledView(f.RoomId, 0, 0), await admin.DisableRoomAsync(f.RoomId, Guid.NewGuid(), "Gọi lại", Ct));
        db.ChangeTracker.Clear();
        Assert.False((await db.Set<ServiceStation>().SingleAsync(x => x.Id == station.Id, Ct)).IsActive);
        Assert.False((await db.Set<MedicalServiceRoom>().SingleAsync(x => x.Id == mapping.Id, Ct)).IsActive);
        Assert.Equal(QueueStatus.InProgress, (await db.Set<QueueTicket>().SingleAsync(x => x.Id == ticket.Id, Ct)).QueueStatus);
        Assert.True(await db.Set<ActiveStationAllocation>().AnyAsync(x => x.StationId == station.Id, Ct));
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().CountAsync(x => x.RecordId == station.Id || x.RecordId == mapping.Id, Ct));
        Assert.Equal(SessionAdminError.RoomNotFound, (await Assert.ThrowsAsync<SessionAdminException>(() => admin.DisableRoomAsync(Guid.NewGuid(), Guid.NewGuid(), "Test", Ct))).Error);
    }

    [Fact]
    public async Task Publisher_failure_rolls_back_doctor_claims_and_audit()
    {
        await using var f = await Fixture.CreateAsync();
        var cs = TestDatabase.RequiredConnectionString();
        await using var services = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton<IDbClock>(f.Clock).AddSingleton<IIdentityPatientsModule>(f.Directory)
            .AddSingleton<IIntegrationEventPublisher, FailingPublisher>().BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await Assert.ThrowsAsync<PublishFailure>(() => scope.ServiceProvider.GetRequiredService<ISessionAdministration>()
            .ReplaceDoctorAsync(f.Directory.Original, Fixture.Date, f.Directory.Replacement, Guid.NewGuid(), "Test rollback", Ct));
        await using var read = f.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        Assert.Equal(f.Directory.Original, (await db.Set<DoctorDayQuota>().SingleAsync(x => x.Id == f.QuotaId, Ct)).DoctorId);
        Assert.All(await db.Set<ActiveBookingClaim>().Where(x => x.SessionQuotaId == f.QuotaId).ToListAsync(Ct), x => Assert.Equal(f.Directory.Original, x.DoctorId));
        Assert.False(await read.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().AnyAsync(x => x.RecordId == f.QuotaId, Ct));
    }
    private sealed class PublishFailure : Exception;
    [Fact]
    public async Task Disabling_an_unconfigured_room_blocks_the_queue_default_station_fallback()
    {
        await using var f = await Fixture.CreateAsync();
        await using var scope = f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISessionAdministration>().DisableRoomAsync(f.RoomId, Guid.NewGuid(), "Tắt trước giờ làm", Ct);
        var resolved = await scope.ServiceProvider.GetRequiredService<Scheduling.Application.Queues.IQueueStore>().ResolveStationAsync(f.RoomId, null, "Phòng giả", Ct);
        Assert.Null(resolved.StationId);
        var station = Assert.Single(await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Set<ServiceStation>().Where(x => x.RoomId == f.RoomId).ToListAsync(Ct));
        Assert.False(station.IsActive);
    }
    private sealed class FailingPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent e, Guid aggregateId, CancellationToken ct) where TEvent : IIntegrationEvent => throw new PublishFailure();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public static readonly DateOnly Date = new(2099, 3, 1);
        public Guid QuotaId { get; } = Guid.NewGuid();
        public Guid AppointmentId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid EncounterId { get; } = Guid.NewGuid();
        public Guid HeldAttemptId { get; private set; }
        public List<Guid> ExtraQuotaIds { get; } = [];
        public Doctors Directory { get; } = new();
        public Clock Clock { get; } = new();
        public ServiceProvider Services { get; private set; } = null!;
        public DoctorDayQuota Day(Guid doctor) => new() { Id = Guid.NewGuid(), DoctorId = doctor, AssignedRoomId = RoomId, ServiceDate = Date,
            Capacity = 30, SessionStatus = SessionStatus.Scheduled, IsOpen = true, SessionStartTimeUtc = Clock.Now.AddMinutes(90),
            BookingCutoffTimeUtc = Clock.Now.AddHours(4), RescheduleCutoffTimeUtc = Clock.Now, CheckInDeadlineUtc = Clock.Now.AddHours(6) };
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.Directory.RoomId = f.RoomId;
            f.Services = Build(f);
            await using var scope = f.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var day = f.Day(f.Directory.Original); day.Id = f.QuotaId; day.HeldCount = 1; day.AdmittedCount = 1; day.LockedCancelledCount = 2; db.Add(day);
            foreach (var held in new[] { false, true })
            {
                var patient = Guid.NewGuid();
                var attempt = new BookingAttempt { Id = Guid.NewGuid(), AppUserId = Guid.NewGuid(), PatientId = patient, SelectedDoctorId = f.Directory.Original,
                    SelectedDoctorRankId = Guid.NewGuid(), SelectedServiceDate = Date, CurrentStep = CurrentStep.Finalized,
                    Status = held ? BookingAttemptStatus.Held : BookingAttemptStatus.Confirmed, CreatedAtUtc = f.Clock.Now, UpdatedAtUtc = f.Clock.Now };
                var reservation = new Reservation { Id = Guid.NewGuid(), SessionQuotaId = f.QuotaId, BookingAttemptId = attempt.Id, PatientId = patient,
                    AppUserId = attempt.AppUserId, BookingChannel = BookingChannel.Web, ReservedAtUtc = f.Clock.Now, ExpiresAtUtc = f.Clock.Now.AddMinutes(5),
                    Status = held ? ReservationStatus.Held : ReservationStatus.Confirmed };
                db.Add(attempt); db.Add(reservation);
                if (held) f.HeldAttemptId = attempt.Id;
                else db.Add(new Appointment { Id = f.AppointmentId, ReservationId = reservation.Id, SessionQuotaId = f.QuotaId, PatientId = patient,
                    AppointmentNumber = "TEST-" + Guid.NewGuid().ToString("N"), AppliedConsultationFee = 150_000m, Status = AppointmentStatus.Scheduled, ConfirmedAtUtc = f.Clock.Now });
                db.Add(new ActiveBookingClaim { Id = Guid.NewGuid(), SessionQuotaId = f.QuotaId, DoctorId = f.Directory.Original, ServiceDate = Date,
                    PatientId = patient, ReservationId = reservation.Id, AppointmentId = held ? null : f.AppointmentId,
                    ClaimSource = ClaimSource.Web, IsActive = true, CreatedAtUtc = f.Clock.Now });
            }
            await db.SaveChangesAsync(Ct);
            return f;
        }
        private static ServiceProvider Build(Fixture f) => new ServiceCollection()
            .AddSharedInfrastructure(TestDatabase.RequiredConnectionString()).AddSchedulingInfrastructure(TestDatabase.RequiredConnectionString())
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton<IDbClock>(f.Clock).AddSingleton<IIdentityPatientsModule>(f.Directory).BuildServiceProvider();
        public async Task ReplaceAsync(Guid replacement)
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISessionAdministration>().ReplaceDoctorAsync(Directory.Original, Date, replacement, Guid.NewGuid(), "Thay ca giả lập", Ct);
        }
        public async Task CancelAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISessionAdministration>().CancelSessionAsync(Directory.Original, Date, Guid.NewGuid(), "Không có người thay", Ct);
        }
        public async ValueTask DisposeAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var shared = scope.ServiceProvider.GetRequiredService<SharedDbContext>();
            var quotas = ExtraQuotaIds.Append(QuotaId).ToArray();
            var appointments = await db.Set<Appointment>().Where(x => quotas.Contains(x.SessionQuotaId)).Select(x => x.Id).ToListAsync(Ct);
            var attempts = await db.Set<Reservation>().Where(x => quotas.Contains(x.SessionQuotaId)).Select(x => x.BookingAttemptId).ToListAsync(Ct);
            var stations = await db.Set<ServiceStation>().Where(x => x.RoomId == RoomId).Select(x => x.Id).ToListAsync(Ct);
            var mappings = await db.Set<MedicalServiceRoom>().Where(x => x.RoomId == RoomId).Select(x => x.Id).ToListAsync(Ct);
            var services = await db.Set<MedicalService>().Where(x => x.DefaultRoomId == RoomId).Select(x => x.Id).ToListAsync(Ct);
            await shared.Set<OutboxMessage>().Where(x => appointments.Contains(x.AggregateId)).ExecuteDeleteAsync(Ct);
            await shared.Set<AuditLog>().Where(x => quotas.Contains(x.RecordId) || appointments.Contains(x.RecordId) || stations.Contains(x.RecordId) || mappings.Contains(x.RecordId)).ExecuteDeleteAsync(Ct);
            await db.Set<ActiveStationAllocation>().Where(x => stations.Contains(x.StationId)).ExecuteDeleteAsync(Ct);
            await db.Set<QueueTicket>().Where(x => x.EncounterId == EncounterId).ExecuteDeleteAsync(Ct);
            await db.Set<ActiveBookingClaim>().Where(x => quotas.Contains(x.SessionQuotaId)).ExecuteDeleteAsync(Ct);
            await db.Set<Appointment>().Where(x => quotas.Contains(x.SessionQuotaId)).ExecuteDeleteAsync(Ct);
            await db.Set<Reservation>().Where(x => quotas.Contains(x.SessionQuotaId)).ExecuteDeleteAsync(Ct);
            await db.Set<BookingAttempt>().Where(x => attempts.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<DoctorDayQuota>().Where(x => quotas.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceStation>().Where(x => stations.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<MedicalServiceRoom>().Where(x => mappings.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await db.Set<MedicalService>().Where(x => services.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await Services.DisposeAsync();
        }
    }
    private sealed class Clock : IDbClock
    {
        public DateTime Now { get; set; } = DateTime.SpecifyKind(Fixture.Date.ToDateTime(new(22, 0)).AddDays(-1), DateTimeKind.Utc);
        public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(Now);
    }
    private sealed class Doctors : IIdentityPatientsModule
    {
        public int ContactCalls { get; private set; }
        public bool OmitContacts { get; set; }
        public Task<IReadOnlyList<PatientContactDto>> GetPatientContactsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            ContactCalls++;
            Assert.InRange(ids.Count, 0, 100);
            return Task.FromResult<IReadOnlyList<PatientContactDto>>(OmitContacts ? [] : ids.Select(id => new PatientContactDto(id, "Test patient", "*******567")).ToArray());
        }
        public Guid Original { get; } = Guid.NewGuid(); public Guid Replacement { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Task<DoctorDto?> GetDoctorAsync(Guid id, CancellationToken ct = default) => Task.FromResult<DoctorDto?>(id == Original || id == Replacement ? new(id, "Bác sĩ giả", Guid.NewGuid(), "TEST", Guid.NewGuid(), "TEST", true) : null);
        public Task<RoomDto?> GetRoomAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RoomDto?>(id == RoomId ? new(id, "TEST", "Phòng giả", "Laboratory", true) : null);
        public Task<bool> CanAccessPatientAsync(Guid u, Guid p, AccessLevel l = AccessLevel.Booking, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? t = null, bool? a = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid r, DateOnly d, string s, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
