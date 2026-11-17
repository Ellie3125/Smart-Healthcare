using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Seed;

public sealed record SchedulingSeedResult(int ServicesAdded, int RoomsAdded, int StationsAdded);

/// <summary>Development demo data only. Caller obtains rooms from Identity and owns the transaction.</summary>
public static class SchedulingSeed
{
    // Same codes and IDs as the existing development price-list seed; do not create a second catalog.
    private static readonly (int Id, string Code, string Name, ServiceCategory Category, string DefaultRoom, bool Mandatory)[] Services =
    [
        (1, "DEV-XN-CTM", "Tổng phân tích tế bào máu", ServiceCategory.Laboratory, "XN01", true),
        (2, "DEV-XN-SH", "Sinh hóa máu cơ bản", ServiceCategory.Laboratory, "XN01", true),
        (3, "DEV-XQ-NGUC", "Chụp X-quang ngực thẳng", ServiceCategory.Radiology, "XQ01", true),
        (4, "DEV-SA-BUNG", "Siêu âm ổ bụng tổng quát", ServiceCategory.Ultrasound, "SA01", true),
        (5, "DEV-NS-DD", "Nội soi dạ dày", ServiceCategory.Endoscopy, "NS01", false),
    ];

    public static async Task<SchedulingSeedResult> SeedAsync(SchedulingDbContext db, IReadOnlyList<RoomDto> rooms, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Scheduling seed must join the caller's transaction.");
        var expected = new Dictionary<string, string> { ["XN01"] = "Laboratory", ["XQ01"] = "XRay", ["XQ02"] = "XRay",
            ["SA01"] = "Ultrasound", ["SA02"] = "Ultrasound", ["NS01"] = "Endoscopy" };
        var selected = new List<RoomDto>();
        foreach (var (code, type) in expected)
        {
            var matches = rooms.Where(x => x.Code == code).ToArray();
            if (matches.Length != 1 || matches[0].RoomType != type)
                throw new ArgumentException($"Seed requires exactly one {type} room with code {code}.", nameof(rooms));
            selected.Add(matches[0]);
        }
        selected.AddRange(rooms.Where(x => x.RoomType == "Clinical"));
        if (selected.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Code) || x.Code.Length > 57)
            || selected.Select(x => x.Id).Distinct().Count() != selected.Count
            || selected.Select(x => x.Code).Distinct().Count() != selected.Count)
            throw new ArgumentException("Seed rooms require distinct IDs and codes of at most 57 characters.", nameof(rooms));

        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource=N'Scheduling.DemoSeed', @LockMode='Exclusive',
                @LockOwner='Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, 'Could not acquire Scheduling demo seed lock.', 1;
            """, ct);
        var codes = Services.Select(x => x.Code).ToArray();
        var services = await db.Set<MedicalService>().FromSqlRaw("SELECT * FROM scheduling.MedicalService WITH (UPDLOCK,HOLDLOCK)")
            .AsNoTracking().Where(x => codes.Contains(x.ServiceCode)).ToListAsync(ct);
        var serviceIds = services.Select(x => x.Id).ToArray();
        var mappings = await db.Set<MedicalServiceRoom>().AsNoTracking().Where(x => serviceIds.Contains(x.MedicalServiceId)).ToListAsync(ct);
        var stations = await db.Set<ServiceStation>().FromSqlRaw("SELECT * FROM scheduling.ServiceStation WITH (UPDLOCK,HOLDLOCK)")
            .AsNoTracking().ToListAsync(ct);
        var addedServices = 0;
        var addedRooms = 0;
        var addedStations = 0;
        foreach (var definition in Services)
        {
            var service = services.SingleOrDefault(x => x.ServiceCode == definition.Code);
            if (service is null)
            {
                var defaultRoom = selected.Single(x => x.Code == definition.DefaultRoom);
                service = new MedicalService { Id = Guid.Parse($"0a1f0003-0000-4000-8000-{definition.Id:D12}"),
                    ServiceCode = definition.Code, ServiceName = definition.Name, ServiceCategory = definition.Category,
                    DefaultRoomId = defaultRoom.Id, IsMandatoryDefault = definition.Mandatory, IsActive = defaultRoom.IsActive };
                db.Add(service);
                addedServices++;
            }
            var roomType = definition.Category == ServiceCategory.Radiology ? "XRay" : definition.Category.ToString();
            foreach (var room in selected.Where(x => x.RoomType == roomType))
            {
                if (mappings.Any(x => x.MedicalServiceId == service.Id && x.RoomId == room.Id)) continue;
                db.Add(new MedicalServiceRoom { Id = Guid.CreateVersion7(), MedicalServiceId = service.Id, RoomId = room.Id,
                    IsDefaultRoom = service.DefaultRoomId == room.Id, IsActive = service.IsActive && room.IsActive });
                addedRooms++;
            }
        }
        foreach (var room in selected)
        {
            var capacity = room.Code == "XN01" ? 3 : 1;
            for (var position = 1; position <= capacity; position++)
            {
                var code = $"DEV-{room.Code}-{position:D2}";
                if (stations.Any(x => x.StationCode == code)) continue;
                db.Add(new ServiceStation { Id = Guid.CreateVersion7(), RoomId = room.Id, StationCode = code,
                    StationName = $"Vị trí {room.Code}-{position:D2}", IsActive = room.IsActive });
                addedStations++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new(addedServices, addedRooms, addedStations);
    }
}
