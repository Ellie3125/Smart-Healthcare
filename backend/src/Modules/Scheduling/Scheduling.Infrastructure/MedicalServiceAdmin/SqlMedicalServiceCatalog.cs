using System.Text.Json;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.MedicalServiceAdmin;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.MedicalServiceAdmin;

public sealed class SqlMedicalServiceCatalog(SchedulingDbContext db, IIdentityPatientsModule identity,
    AuditLogWriter audit, IDbClock clock, IUnitOfWork uow) : IMedicalServiceCatalog
{
    public async Task<CatalogPage<MedicalServiceView>> GetServicesAsync(bool activeOnly, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize);
        var services = await db.Set<MedicalService>().AsNoTracking().Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.ServiceCode).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = services.Select(x => x.Id).ToArray();
        var mappings = await db.Set<MedicalServiceRoom>().AsNoTracking()
            .Where(x => ids.Contains(x.MedicalServiceId) && (!activeOnly || x.IsActive)).OrderBy(x => x.RoomId).ToListAsync(ct);
        return new(services.Select(x => View(x, mappings.Where(m => m.MedicalServiceId == x.Id))).ToArray(), page, pageSize);
    }

    public Task<MedicalServiceView> CreateServiceAsync(CreateMedicalService r, Guid userId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Actor(userId); Text(r.ServiceCode, 64); Validate(r.ServiceName, r.ServiceCategory);
        await Room(r.DefaultRoomId, r.IsActive, t);
        var code = r.ServiceCode.Trim();
        if (await db.Set<MedicalService>().FromSql($"SELECT * FROM scheduling.MedicalService WITH (UPDLOCK,HOLDLOCK) WHERE ServiceCode={code}").AnyAsync(t))
            throw new CatalogException(CatalogError.CodeAlreadyExists);
        var service = new MedicalService { Id = Guid.CreateVersion7(), ServiceCode = code, ServiceName = r.ServiceName.Trim(),
            ServiceCategory = Enum.Parse<ServiceCategory>(r.ServiceCategory), DefaultRoomId = r.DefaultRoomId,
            IsMandatoryDefault = r.IsMandatoryDefault, IsActive = r.IsActive };
        db.Add(service);
        db.Add(new MedicalServiceRoom { Id = Guid.CreateVersion7(), MedicalServiceId = service.Id,
            RoomId = r.DefaultRoomId, IsDefaultRoom = true, IsActive = r.IsActive });
        await db.SaveChangesAsync(t);
        var result = await ReadService(service.Id, t);
        await Audit("scheduling.MedicalService", service.Id, userId, null, result, t);
        return result;
    }, ct);

    public Task<MedicalServiceView> UpdateServiceAsync(Guid id, UpdateMedicalService r, Guid userId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Actor(userId); Validate(r.ServiceName, r.ServiceCategory);
        await LockService(id, t);
        await Room(r.DefaultRoomId, r.IsActive, t);
        var before = await ReadService(id, t);
        await db.Set<MedicalService>().Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ServiceName, r.ServiceName.Trim()).SetProperty(x => x.ServiceCategory, Enum.Parse<ServiceCategory>(r.ServiceCategory))
            .SetProperty(x => x.DefaultRoomId, r.DefaultRoomId).SetProperty(x => x.IsMandatoryDefault, r.IsMandatoryDefault)
            .SetProperty(x => x.IsActive, r.IsActive), t);
        await db.Set<MedicalServiceRoom>().Where(x => x.MedicalServiceId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefaultRoom, false), t);
        var mapping = await db.Set<MedicalServiceRoom>().AsNoTracking().SingleOrDefaultAsync(x => x.MedicalServiceId == id && x.RoomId == r.DefaultRoomId, t);
        if (mapping is null)
            db.Add(new MedicalServiceRoom { Id = Guid.CreateVersion7(), MedicalServiceId = id, RoomId = r.DefaultRoomId, IsDefaultRoom = true, IsActive = r.IsActive });
        else
            await db.Set<MedicalServiceRoom>().Where(x => x.Id == mapping.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDefaultRoom, true).SetProperty(x => x.IsActive, r.IsActive), t);
        await db.SaveChangesAsync(t);
        var after = await ReadService(id, t);
        await Audit("scheduling.MedicalService", id, userId, before, after, t);
        return after;
    }, ct);

    public Task SetRoomAsync(Guid serviceId, Guid roomId, bool active, Guid userId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Actor(userId);
        var service = await LockService(serviceId, t);
        await Room(roomId, active, t);
        if (!active && service.IsActive && service.DefaultRoomId == roomId)
            throw new CatalogException(CatalogError.DefaultRoomRequired);
        var before = await ReadService(serviceId, t);
        var mapping = await db.Set<MedicalServiceRoom>().AsNoTracking().SingleOrDefaultAsync(x => x.MedicalServiceId == serviceId && x.RoomId == roomId, t);
        if (mapping is null)
            db.Add(new MedicalServiceRoom { Id = Guid.CreateVersion7(), MedicalServiceId = serviceId,
                RoomId = roomId, IsDefaultRoom = service.DefaultRoomId == roomId, IsActive = active });
        else
            await db.Set<MedicalServiceRoom>().Where(x => x.Id == mapping.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, active), t);
        await db.SaveChangesAsync(t);
        await Audit("scheduling.MedicalService", serviceId, userId, before, await ReadService(serviceId, t), t);
    }, ct);

    public async Task<CatalogPage<ServiceStationView>> GetStationsAsync(Guid? roomId, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize);
        if (roomId == Guid.Empty) throw new CatalogException(CatalogError.InvalidInput);
        var rows = await db.Set<ServiceStation>().AsNoTracking().Where(x => roomId == null || x.RoomId == roomId)
            .OrderBy(x => x.StationCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ServiceStationView(x.Id, x.RoomId, x.StationCode, x.StationName, x.IsActive)).ToListAsync(ct);
        return new(rows, page, pageSize);
    }

    public Task<ServiceStationView> CreateStationAsync(CreateServiceStation r, Guid userId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Actor(userId); Text(r.StationCode, 64); Text(r.StationName, 200);
        await Room(r.RoomId, r.IsActive, t);
        var code = r.StationCode.Trim();
        if (await db.Set<ServiceStation>().FromSql($"SELECT * FROM scheduling.ServiceStation WITH (UPDLOCK,HOLDLOCK) WHERE StationCode={code}").AnyAsync(t))
            throw new CatalogException(CatalogError.CodeAlreadyExists);
        var station = new ServiceStation { Id = Guid.CreateVersion7(), RoomId = r.RoomId, StationCode = code,
            StationName = r.StationName.Trim(), IsActive = r.IsActive };
        db.Add(station);
        await db.SaveChangesAsync(t);
        var result = View(station);
        await Audit("scheduling.ServiceStation", station.Id, userId, null, result, t);
        return result;
    }, ct);

    public Task<ServiceStationView> UpdateStationAsync(Guid id, UpdateServiceStation r, Guid userId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Actor(userId); Text(r.StationName, 200);
        var station = await db.Set<ServiceStation>().FromSql($"SELECT * FROM scheduling.ServiceStation WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().SingleOrDefaultAsync(t) ?? throw new CatalogException(CatalogError.StationNotFound);
        await Room(station.RoomId, r.IsActive, t);
        var before = View(station);
        // Keep allocation and ticket rows intact so a patient already being served can finish.
        await db.Set<ServiceStation>().Where(x => x.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.StationName, r.StationName.Trim()).SetProperty(x => x.IsActive, r.IsActive), t);
        var after = before with { StationName = r.StationName.Trim(), IsActive = r.IsActive };
        await Audit("scheduling.ServiceStation", id, userId, before, after, t);
        return after;
    }, ct);

    private async Task<MedicalService> LockService(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Catalog writes require IUnitOfWork.");
        return await db.Set<MedicalService>().FromSql($"SELECT * FROM scheduling.MedicalService WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new CatalogException(CatalogError.ServiceNotFound);
    }

    private async Task<MedicalServiceView> ReadService(Guid id, CancellationToken ct) => View(
        await db.Set<MedicalService>().AsNoTracking().SingleAsync(x => x.Id == id, ct),
        await db.Set<MedicalServiceRoom>().AsNoTracking().Where(x => x.MedicalServiceId == id).OrderBy(x => x.RoomId).ToListAsync(ct));

    private async Task Room(Guid id, bool requireActive, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new CatalogException(CatalogError.InvalidInput);
        var room = await identity.GetRoomAsync(id, ct);
        if (room is null || (requireActive && !room.IsActive)
            || room.RoomType is not ("Laboratory" or "XRay" or "Ultrasound" or "Endoscopy"))
            throw new CatalogException(CatalogError.RoomNotFound);
    }

    private async Task Audit(string table, Guid id, Guid userId, object? before, object after, CancellationToken ct) =>
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = userId, TableName = table, RecordId = id,
            ActionType = before is null ? ActionType.INSERT : ActionType.UPDATE,
            ChangedColumns = table == "scheduling.ServiceStation" ? "RoomId,StationCode,StationName,IsActive" : "ServiceCode,ServiceName,ServiceCategory,DefaultRoomId,IsMandatoryDefault,IsActive,Rooms",
            OldValuesJson = before is null ? null : JsonSerializer.Serialize(before), NewValuesJson = JsonSerializer.Serialize(after),
            TimestampUtc = await clock.GetUtcNowAsync(ct) }, ct);

    private static MedicalServiceView View(MedicalService x, IEnumerable<MedicalServiceRoom> rooms) => new(x.Id, x.ServiceCode, x.ServiceName,
        x.ServiceCategory.ToString(), x.DefaultRoomId, x.IsMandatoryDefault, x.IsActive,
        rooms.Select(m => new ServiceRoomView(m.RoomId, m.IsDefaultRoom, m.IsActive)).ToArray());
    private static ServiceStationView View(ServiceStation x) => new(x.Id, x.RoomId, x.StationCode, x.StationName, x.IsActive);
    private static void Text(string value, int max) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new CatalogException(CatalogError.InvalidInput); }
    private static void Actor(Guid id) { if (id == Guid.Empty) throw new CatalogException(CatalogError.InvalidInput); }
    private static void Page(int page, int size) { if (page < 1 || size is < 1 or > 100 || page > int.MaxValue / size) throw new CatalogException(CatalogError.InvalidInput); }
    private static void Validate(string name, string category)
    {
        Text(name, 200);
        if (!Enum.GetNames<ServiceCategory>().Contains(category)) throw new CatalogException(CatalogError.InvalidInput);
    }
}
