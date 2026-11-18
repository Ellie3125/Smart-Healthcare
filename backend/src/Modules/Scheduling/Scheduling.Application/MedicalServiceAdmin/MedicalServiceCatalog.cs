namespace Scheduling.Application.MedicalServiceAdmin;

public sealed record CreateMedicalService(string ServiceCode, string ServiceName, string ServiceCategory,
    Guid DefaultRoomId, bool IsMandatoryDefault, bool IsActive);
public sealed record UpdateMedicalService(string ServiceName, string ServiceCategory, Guid DefaultRoomId, bool IsMandatoryDefault, bool IsActive);
public sealed record CreateServiceStation(Guid RoomId, string StationCode, string StationName, bool IsActive);
public sealed record UpdateServiceStation(string StationName, bool IsActive);
public sealed record ServiceRoomView(Guid RoomId, bool IsDefaultRoom, bool IsActive);
public sealed record MedicalServiceView(Guid Id, string ServiceCode, string ServiceName, string ServiceCategory,
    Guid DefaultRoomId, bool IsMandatoryDefault, bool IsActive, IReadOnlyList<ServiceRoomView> Rooms);
public sealed record ServiceStationView(Guid Id, Guid RoomId, string StationCode, string StationName, bool IsActive);
public sealed record CatalogPage<T>(IReadOnlyList<T> Items, int Page, int PageSize);
public enum CatalogError { InvalidInput, RoomNotFound, ServiceNotFound, StationNotFound, CodeAlreadyExists, DefaultRoomRequired }
public sealed class CatalogException(CatalogError error) : Exception(error.ToString())
{
    public CatalogError Error { get; } = error;
}

public interface IMedicalServiceCatalog
{
    Task<CatalogPage<MedicalServiceView>> GetServicesAsync(bool activeOnly, int page, int pageSize, CancellationToken ct);
    Task<MedicalServiceView> CreateServiceAsync(CreateMedicalService request, Guid userId, CancellationToken ct);
    Task<MedicalServiceView> UpdateServiceAsync(Guid id, UpdateMedicalService request, Guid userId, CancellationToken ct);
    Task SetRoomAsync(Guid serviceId, Guid roomId, bool active, Guid userId, CancellationToken ct);
    Task<CatalogPage<ServiceStationView>> GetStationsAsync(Guid? roomId, int page, int pageSize, CancellationToken ct);
    Task<ServiceStationView> CreateStationAsync(CreateServiceStation request, Guid userId, CancellationToken ct);
    Task<ServiceStationView> UpdateStationAsync(Guid id, UpdateServiceStation request, Guid userId, CancellationToken ct);
}
