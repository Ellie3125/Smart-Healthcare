using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application.MedicalServiceAdmin;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure;
using Scheduling.Infrastructure.MedicalServiceAdmin;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class MedicalServiceAdminTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static CreateMedicalService Request(Guid room) => new("TEST-" + Guid.NewGuid().ToString("N"), "Dịch vụ giả lập", "Laboratory", room, true, true);

    [Fact]
    public async Task Service_and_default_mapping_change_together_and_staff_only_sees_active_services()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var identity = new Rooms();
        var catalog = Catalog(db, shared, identity);
        var created = await catalog.CreateServiceAsync(Request(identity.First), Guid.NewGuid(), Ct);
        Assert.Equal(identity.First, Assert.Single(created.Rooms).RoomId);
        Assert.True(created.Rooms[0].IsDefaultRoom);
        var updated = await catalog.UpdateServiceAsync(created.Id, new("Đã sửa", "Laboratory", identity.Second, false, true), Guid.NewGuid(), Ct);
        Assert.Equal(identity.Second, updated.DefaultRoomId);
        Assert.Single(updated.Rooms, x => x.IsDefaultRoom);
        Assert.Equal(identity.Second, updated.Rooms.Single(x => x.IsDefaultRoom).RoomId);
        var error = await Assert.ThrowsAsync<CatalogException>(() => catalog.SetRoomAsync(created.Id, identity.Second, false, Guid.NewGuid(), Ct));
        Assert.Equal(CatalogError.DefaultRoomRequired, error.Error);
        await catalog.SetRoomAsync(created.Id, identity.First, false, Guid.NewGuid(), Ct);
        Assert.Contains((await catalog.GetServicesAsync(true, 1, 100, Ct)).Items, x => x.Id == created.Id);
        await catalog.UpdateServiceAsync(created.Id, new("Đã sửa", "Laboratory", identity.Second, false, false), Guid.NewGuid(), Ct);
        Assert.DoesNotContain((await catalog.GetServicesAsync(true, 1, 100, Ct)).Items, x => x.Id == created.Id);
        Assert.Contains((await catalog.GetServicesAsync(false, 1, 100, Ct)).Items, x => x.Id == created.Id);
        Assert.Equal(4, await shared.Set<AuditLog>().CountAsync(x => x.RecordId == created.Id, Ct));
    }

    [Fact]
    public async Task Disabling_station_keeps_the_allocation_and_in_progress_ticket()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var identity = new Rooms();
        var catalog = Catalog(db, shared, identity);
        var station = await catalog.CreateStationAsync(new(identity.First, "TEST-" + Guid.NewGuid().ToString("N"), "Bàn 1", true), Guid.NewGuid(), Ct);
        var ticket = new QueueTicket { Id = Guid.NewGuid(), EncounterId = Guid.NewGuid(), TargetRoomId = identity.First,
            TargetStationId = station.Id, TicketNumber = Guid.NewGuid().ToString("N"), SequenceNumber = 1,
            TicketType = TicketType.CLS, QueueStatus = QueueStatus.InProgress, IssuedAtUtc = DateTime.UtcNow };
        db.Add(ticket);
        await db.SaveChangesAsync(Ct);
        await fixture.InsertAsync("scheduling.ActiveStationAllocation", new { StationId = station.Id, CurrentTicketId = ticket.Id });
        await catalog.UpdateStationAsync(station.Id, new("Bàn tạm ngừng", false), Guid.NewGuid(), Ct);
        db.ChangeTracker.Clear();
        Assert.False((await db.Set<ServiceStation>().SingleAsync(x => x.Id == station.Id, Ct)).IsActive);
        Assert.Equal(QueueStatus.InProgress, (await db.Set<QueueTicket>().SingleAsync(x => x.Id == ticket.Id, Ct)).QueueStatus);
        Assert.Single(await fixture.QueryAsync($"SELECT StationId FROM scheduling.ActiveStationAllocation WHERE StationId='{station.Id}'"));
    }

    [Theory]
    [InlineData("", "Laboratory")]
    [InlineData("Valid", "0")]
    [InlineData("Valid", "Unknown")]
    public async Task Invalid_service_inputs_are_rejected_before_writing(string name, string category)
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var rooms = new Rooms();
        var error = await Assert.ThrowsAsync<CatalogException>(() => Catalog(db, shared, rooms)
            .CreateServiceAsync(Request(rooms.First) with { ServiceName = name, ServiceCategory = category }, Guid.NewGuid(), Ct));
        Assert.Equal(CatalogError.InvalidInput, error.Error);
    }

    [Fact]
    public async Task Missing_objects_and_rooms_have_specific_errors()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var rooms = new Rooms();
        var catalog = Catalog(db, shared, rooms);
        Assert.Equal(CatalogError.RoomNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateServiceAsync(Request(Guid.NewGuid()), Guid.NewGuid(), Ct))).Error);
        Assert.Equal(CatalogError.ServiceNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.UpdateServiceAsync(Guid.NewGuid(), new("Test", "Laboratory", rooms.First, false, true), Guid.NewGuid(), Ct))).Error);
        Assert.Equal(CatalogError.StationNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.UpdateStationAsync(Guid.NewGuid(), new("Test", false), Guid.NewGuid(), Ct))).Error);
    }

    [Fact]
    public async Task Two_creators_with_same_code_produce_one_service_and_one_default_mapping()
    {
        var rooms = new Rooms();
        var request = Request(rooms.First);
        var cs = TestDatabase.RequiredConnectionString();
        await using var services = new ServiceCollection().AddSharedInfrastructure(cs).AddSchedulingInfrastructure(cs)
            .AddUnitOfWorkParticipant<SchedulingDbContext>().AddSingleton<IIdentityPatientsModule>(rooms).BuildServiceProvider();
        try
        {
            async Task<string> Create()
            {
                await using var scope = services.CreateAsyncScope();
                try { await scope.ServiceProvider.GetRequiredService<IMedicalServiceCatalog>().CreateServiceAsync(request, Guid.NewGuid(), Ct); return "Created"; }
                catch (CatalogException error) { return error.Error.ToString(); }
            }
            var results = await Task.WhenAll(Create(), Create());
            Assert.Single(results, x => x == "Created");
            Assert.Single(results, x => x == "CodeAlreadyExists");
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var service = await db.Set<MedicalService>().SingleAsync(x => x.ServiceCode == request.ServiceCode, Ct);
            Assert.Equal(1, await db.Set<MedicalServiceRoom>().CountAsync(x => x.MedicalServiceId == service.Id, Ct));
        }
        finally
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var ids = await db.Set<MedicalService>().Where(x => x.ServiceCode == request.ServiceCode).Select(x => x.Id).ToListAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<SharedDbContext>().Set<AuditLog>().Where(x => ids.Contains(x.RecordId)).ExecuteDeleteAsync(Ct);
            await db.Set<MedicalServiceRoom>().Where(x => ids.Contains(x.MedicalServiceId)).ExecuteDeleteAsync(Ct);
            await db.Set<MedicalService>().Where(x => x.ServiceCode == request.ServiceCode).ExecuteDeleteAsync(Ct);
        }
    }

    private static SqlMedicalServiceCatalog Catalog(SchedulingDbContext db, SharedDbContext shared, Rooms rooms) =>
        new(db, rooms, new AuditLogWriter(shared), new Clock(), new JoinedTransaction());

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(2147483647, 100)]
    public async Task Lists_reject_invalid_or_overflowing_pages(int page, int size)
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var catalog = Catalog(db, shared, new Rooms());
        Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.GetServicesAsync(false, page, size, Ct))).Error);
        Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.GetStationsAsync(null, page, size, Ct))).Error);
    }

    [Fact]
    public async Task Station_code_is_unique_and_field_limits_and_room_access_are_enforced()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        await using var shared = fixture.CreateContext<SharedDbContext>(o => new(o));
        var rooms = new Rooms();
        var catalog = Catalog(db, shared, rooms);
        var request = new CreateServiceStation(rooms.First, "TEST-" + Guid.NewGuid().ToString("N"), "Bàn 1", true);
        var station = await catalog.CreateStationAsync(request, Guid.NewGuid(), Ct);
        Assert.Equal(CatalogError.CodeAlreadyExists, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateStationAsync(request, Guid.NewGuid(), Ct))).Error);
        foreach (var invalid in new[] { request with { StationCode = new('x', 65) }, request with { StationName = new('x', 201) }, request with { RoomId = Guid.Empty } })
            Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateStationAsync(invalid, Guid.NewGuid(), Ct))).Error);
        Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateStationAsync(request, Guid.Empty, Ct))).Error);
        Assert.Equal(CatalogError.RoomNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateStationAsync(request with { RoomId = Guid.NewGuid() }, Guid.NewGuid(), Ct))).Error);
        Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.GetStationsAsync(Guid.Empty, 1, 50, Ct))).Error);
        Assert.Equal(station, Assert.Single((await catalog.GetStationsAsync(rooms.First, 1, 50, Ct)).Items));
        Assert.Empty((await catalog.GetStationsAsync(rooms.Second, 1, 50, Ct)).Items);
        Assert.Equal(CatalogError.InvalidInput, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateServiceAsync(Request(rooms.First) with { ServiceCode = new('x', 65) }, Guid.NewGuid(), Ct))).Error);
        rooms.RoomType = "Clinical";
        Assert.Equal(CatalogError.RoomNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.CreateServiceAsync(Request(rooms.First), Guid.NewGuid(), Ct))).Error);
        rooms.RoomType = "Laboratory";
        rooms.Active = false;
        Assert.Equal(CatalogError.RoomNotFound, (await Assert.ThrowsAsync<CatalogException>(() => catalog.UpdateStationAsync(station.Id, new("Bàn 1", true), Guid.NewGuid(), Ct))).Error);
        Assert.False((await catalog.UpdateStationAsync(station.Id, new("Bàn 1", false), Guid.NewGuid(), Ct)).IsActive);
    }

    private sealed class Clock : IDbClock { public Task<DateTime> GetUtcNowAsync(CancellationToken ct) => Task.FromResult(DateTime.UtcNow); }
    private sealed class JoinedTransaction : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
    private sealed class Rooms : IIdentityPatientsModule
    {
        public Guid First { get; } = Guid.NewGuid();
        public Guid Second { get; } = Guid.NewGuid();
        public string RoomType { get; set; } = "Laboratory";
        public bool Active { get; set; } = true;
        public Task<RoomDto?> GetRoomAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RoomDto?>(id == First || id == Second ? new(id, "TEST", "Phòng giả", RoomType, Active) : null);
        public Task<bool> CanAccessPatientAsync(Guid u, Guid p, AccessLevel l = AccessLevel.Booking, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DoctorDto?> GetDoctorAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch q, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? t = null, bool? a = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid r, DateOnly d, string s, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
