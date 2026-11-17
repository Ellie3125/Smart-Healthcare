using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;
using Scheduling.Infrastructure.Seed;
using Xunit;

namespace SqlServerTests;

[Collection(nameof(SharedInfrastructureSerialCollection))]
public sealed class SchedulingSeedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static RoomDto[] Rooms() => new[] { ("XN01", "Laboratory"), ("XQ01", "XRay"), ("XQ02", "XRay"),
        ("SA01", "Ultrasound"), ("SA02", "Ultrasound"), ("NS01", "Endoscopy") }
        .Concat(Enumerable.Range(1, 10).Select(i => ($"PK{i:D2}", "Clinical")))
        .Select(x => new RoomDto(Guid.NewGuid(), x.Item1, "Phòng giả " + x.Item1, x.Item2, true)).ToArray();

    [Fact]
    public async Task Seeds_demo_catalog_mappings_and_capacity_without_overwriting_admin_changes()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        var rooms = Rooms();
        Assert.Equal(new SchedulingSeedResult(5, 7, 18), await SchedulingSeed.SeedAsync(db, rooms, Ct));
        db.ChangeTracker.Clear();
        var service = await db.Set<MedicalService>().SingleAsync(x => x.ServiceCode == "DEV-XN-CTM", Ct);
        await db.Set<MedicalService>().Where(x => x.Id == service.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.ServiceName, "Admin đã sửa"), Ct);
        var station = await db.Set<ServiceStation>().SingleAsync(x => x.StationCode == "DEV-XN01-01", Ct);
        await db.Set<ServiceStation>().Where(x => x.Id == station.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), Ct);
        Assert.Equal(new SchedulingSeedResult(0, 0, 0), await SchedulingSeed.SeedAsync(db, rooms, Ct));
        Assert.Equal("Admin đã sửa", (await db.Set<MedicalService>().AsNoTracking().SingleAsync(x => x.Id == service.Id, Ct)).ServiceName);
        Assert.False((await db.Set<ServiceStation>().AsNoTracking().SingleAsync(x => x.Id == station.Id, Ct)).IsActive);
        var labId = rooms.Single(r => r.Code == "XN01").Id;
        Assert.Equal(3, await db.Set<ServiceStation>().CountAsync(x => x.RoomId == labId, Ct));
    }

    [Fact]
    public async Task Missing_or_wrong_type_rooms_fail_before_any_insert()
    {
        await using var fixture = await TestDatabase.OpenAsync();
        await using var db = fixture.CreateContext<SchedulingDbContext>(o => new(o));
        var rooms = Rooms();
        await Assert.ThrowsAsync<ArgumentException>(() => SchedulingSeed.SeedAsync(db, rooms.Where(x => x.Code != "XN01").ToArray(), Ct));
        rooms[0] = rooms[0] with { RoomType = "Clinical" };
        await Assert.ThrowsAsync<ArgumentException>(() => SchedulingSeed.SeedAsync(db, rooms, Ct));
        Assert.Equal(0, await db.Set<MedicalService>().CountAsync(x => x.ServiceCode.StartsWith("DEV-"), Ct));
    }

    [Fact]
    public async Task Requires_transaction_and_does_not_create_a_private_commit()
    {
        var options = new DbContextOptionsBuilder<SchedulingDbContext>().UseSqlServer(TestDatabase.RequiredConnectionString()).Options;
        await using var db = new SchedulingDbContext(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SchedulingSeed.SeedAsync(db, Rooms(), Ct));
        var roomIds = Rooms();
        await using (var fixture = await TestDatabase.OpenAsync())
        {
            await using var transactionDb = fixture.CreateContext<SchedulingDbContext>(o => new(o));
            await SchedulingSeed.SeedAsync(transactionDb, roomIds, Ct);
        }
        Assert.Equal(0, await db.Set<ServiceStation>().CountAsync(x => roomIds.Select(r => r.Id).Contains(x.RoomId), Ct));
    }

    [Fact]
    public async Task Two_seeders_serialize_and_only_one_adds_the_demo_catalog()
    {
        var rooms = Rooms();
        var options = new DbContextOptionsBuilder<SchedulingDbContext>().UseSqlServer(TestDatabase.RequiredConnectionString()).Options;
        await using var initial = new SchedulingDbContext(options);
        var existingIds = await initial.Set<MedicalService>().Select(x => x.Id).ToListAsync(Ct);
        try
        {
            async Task<SchedulingSeedResult> Seed()
            {
                await using var db = new SchedulingDbContext(options);
                await using var transaction = await db.Database.BeginTransactionAsync(Ct);
                var result = await SchedulingSeed.SeedAsync(db, rooms, Ct);
                await transaction.CommitAsync(Ct);
                return result;
            }
            var results = await Task.WhenAll(Seed(), Seed());
            Assert.Contains(new SchedulingSeedResult(5, 7, 18), results);
            Assert.Contains(new SchedulingSeedResult(0, 0, 0), results);
        }
        finally
        {
            await using var db = new SchedulingDbContext(options);
            var ids = await db.Set<MedicalService>().Where(x => x.ServiceCode.StartsWith("DEV-") && !existingIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(Ct);
            var roomIds = rooms.Select(x => x.Id).ToArray();
            await db.Set<MedicalServiceRoom>().Where(x => ids.Contains(x.MedicalServiceId) && roomIds.Contains(x.RoomId)).ExecuteDeleteAsync(Ct);
            await db.Set<ServiceStation>().Where(x => roomIds.Contains(x.RoomId)).ExecuteDeleteAsync(Ct);
            await db.Set<MedicalService>().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(Ct);
        }
    }
}
