using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.PatientNotifications;
using Notifications.Contracts;
using Notifications.Domain.Entities;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Scheduling.Contracts;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

public sealed class PatientNotificationMessageTests
{
    [Theory]
    [InlineData("AppointmentConfirmed", "Appointment")]
    [InlineData("AppointmentCancelled", "Appointment")]
    [InlineData("DoctorChanged", "Appointment")]
    [InlineData("ClsOrdered", "Clinical")]
    [InlineData("ResultsAvailable", "Clinical")]
    [InlineData("TicketCalled", "Queue")]
    [InlineData("ReturnTicketActivated", "Queue")]
    public void Messages_use_client_categories_and_contain_no_result_details(string kind, string category)
    {
        var notification = new PatientNotification { Id = Guid.NewGuid(), Kind = Enum.Parse<Notifications.Domain.Enums.PatientNotificationKind>(kind), CreatedAtUtc = DateTime.UtcNow };
        var sent = PatientNotificationMessages.Create(notification, false);
        Assert.Equal(category, sent.Category); Assert.Equal("Sent", sent.Status);
        Assert.False(string.IsNullOrWhiteSpace(sent.Title)); Assert.False(string.IsNullOrWhiteSpace(sent.Body));
        Assert.Null(sent.TargetPath);
        Assert.Equal("Read", PatientNotificationMessages.Create(notification, true).Status);
    }
}

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class PatientNotificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_delivery_and_read_are_idempotent_and_read_state_is_per_user()
    {
        await using var f = await Fixture.CreateAsync();
        var command = f.Command(PatientNotificationKind.AppointmentConfirmed);
        var writes = await Task.WhenAll(f.Write(command), f.Write(command));
        Assert.Equal(writes[0], writes[1]);
        var reads = await Task.WhenAll(f.Read(f.Users.First, writes[0]), f.Read(f.Users.First, writes[0]));
        Assert.All(reads, Assert.True);
        var first = await f.List(f.Users.First); var second = await f.List(f.Users.Second);
        Assert.Equal("Read", Assert.Single(first.Items).Status); Assert.Equal(0, first.UnreadCount);
        Assert.Equal("Sent", Assert.Single(second.Items).Status); Assert.Equal(1, second.UnreadCount);
        Assert.False(await f.Read(Guid.NewGuid(), writes[0]));
        await Assert.ThrowsAsync<PatientNotificationException>(() => f.List(Guid.NewGuid()));
        Assert.Equal(PatientNotificationError.IdempotencyConflict,
            (await Assert.ThrowsAsync<PatientNotificationException>(() => f.Write(command with { TargetEntityId = Guid.NewGuid() }))).Error);
    }

    [Fact]
    public async Task Results_stay_hidden_until_verified_access_and_post_cls_conclusion_are_both_available()
    {
        await using var f = await Fixture.CreateAsync();
        var id = await f.Write(f.Command(PatientNotificationKind.ResultsAvailable));
        Assert.Empty((await f.List(f.Users.First)).Items); Assert.False(await f.Read(f.Users.First, id));
        f.Clinical.Published = true;
        Assert.Single((await f.List(f.Users.First)).Items);
        f.Users.Verified = false;
        Assert.Empty((await f.List(f.Users.First)).Items); Assert.False(await f.Read(f.Users.First, id));
    }

    [Fact]
    public async Task Appointment_handler_replay_does_not_duplicate_or_reset_a_read_notification()
    {
        await using var f = await Fixture.CreateAsync();
        var eventId = Guid.NewGuid(); f.Events.Add(eventId);
        var e = new AppointmentConfirmed(eventId, Guid.NewGuid(), Guid.NewGuid(), f.Users.Patient, Guid.NewGuid(), new(2099, 1, 1), "TEST", null, DateTime.UtcNow, null);
        async Task Deliver()
        {
            await using var scope = f.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<AppointmentConfirmed>>().HandleAsync(e, Ct);
        }
        await Task.WhenAll(Deliver(), Deliver());
        var item = Assert.Single((await f.List(f.Users.First)).Items);
        Assert.True(await f.Read(f.Users.First, item.Id)); await Deliver();
        Assert.Equal("Read", Assert.Single((await f.List(f.Users.First)).Items).Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Users Users { get; } = new(); public Clinical Clinical { get; } = new();
        public List<Guid> Events { get; } = []; public ServiceProvider Services { get; private set; } = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); var cs = TestDatabase.RequiredConnectionString();
            f.Services = new ServiceCollection().AddSharedInfrastructure(cs).AddNotificationsInfrastructure(cs)
                .AddUnitOfWorkParticipant<WorkflowDbContext>().AddSingleton<IIdentityPatientsModule>(f.Users).AddSingleton<IClinicalModule>(f.Clinical).BuildServiceProvider();
            await using var scope = f.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var ready = await db.Database.SqlQuery<int>($"SELECT IIF(OBJECT_ID(N'workflow.PatientNotification') IS NOT NULL AND OBJECT_ID(N'workflow.PatientNotificationRead') IS NOT NULL,1,0) AS Value").SingleAsync(Ct);
            Assert.True(ready == 1, "Cần owner áp migration PatientNotification và PatientNotificationRead trước khi chạy SQL test này.");
            return f;
        }
        public CreatePatientNotification Command(PatientNotificationKind kind)
        {
            var e = Guid.NewGuid(); Events.Add(e); return new(e, Users.Patient, kind, Guid.NewGuid());
        }
        public async Task<Guid> Write(CreatePatientNotification c)
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<IPatientNotificationWriter>().SaveAsync(c, Ct); }
        public async Task<bool> Read(Guid user, Guid id)
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<IPatientNotificationReader>().MarkReadAsync(user, id, Ct); }
        public async Task<PatientNotificationPage> List(Guid user)
        { await using var s = Services.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<IPatientNotificationReader>().ListAsync(user, Users.Patient, false, 1, 50, Ct); }
        public async ValueTask DisposeAsync()
        {
            await using var scope = Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var ids = await db.Set<PatientNotification>().Where(x => Events.Contains(x.SourceEventId)).Select(x => x.Id).ToListAsync(Ct);
            await db.Set<PatientNotificationRead>().Where(x => ids.Contains(x.NotificationId)).ExecuteDeleteAsync(Ct);
            await db.Set<PatientNotification>().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<ProcessedEvent>().Where(x => Events.Contains(x.EventId)).ExecuteDeleteAsync(Ct);
            await Services.DisposeAsync();
        }
    }
    private sealed class Users : IIdentityPatientsModule
    {
        public Guid First { get; } = Guid.NewGuid(); public Guid Second { get; } = Guid.NewGuid(); public Guid Patient { get; } = Guid.NewGuid(); public bool Verified { get; set; } = true;
        public Task<bool> CanAccessPatientAsync(Guid u, Guid p, AccessLevel l = AccessLevel.Booking, CancellationToken ct = default) => Task.FromResult(p == Patient && (u == First || u == Second) && (l == AccessLevel.Booking || Verified));
        public Task<DoctorDto?> GetDoctorAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RoomDto?> GetRoomAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? t = null, bool? a = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid r, DateOnly d, string s, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Clinical : IClinicalModule
    {
        public bool Published { get; set; }
        public Task<bool> IsPostClsConclusionPublishedAsync(Guid id, CancellationToken ct) => Task.FromResult(Published);
        public Task<EncounterDto?> GetEncounterByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterDto> StartEncounterAsync(StartEncounterCommand c, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveEncounterAsync(Guid p, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<EncounterSummaryDto>> GetPatientEncountersAsync(Guid p, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterClinicalRecordDto?> GetClinicalRecordByEncounterIdAsync(Guid e, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ServiceOrderDto>> GetServiceOrdersByEncounterIdAsync(Guid e, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PrescriptionDto>> GetPrescriptionsByEncounterIdAsync(Guid e, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsOriginalDoctorAsync(Guid d, Guid e, CancellationToken ct) => throw new NotSupportedException();
    }
}
