using IdentityPatients.Application.Common;

namespace IdentityPatients.Application.Rooms;

public sealed record CreateRoomAccountRequest(
    string UserName,
    string Email,
    string Password,
    string AccountCode);

public sealed record RoomAccountDto(
    Guid RoomAccountId,
    Guid RoomId,
    Guid AppUserId,
    string AccountCode,
    string UserName,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record RoomSessionDto(
    Guid SessionId,
    Guid RoomAccountId,
    string DeviceId,
    string DeviceIpAddress,
    DateTime LoginAtUtc,
    DateTime? LogoutAtUtc,
    bool IsActive);

public interface IRoomManagementService
{
    Task<ServiceResult<RoomAccountDto>> CreateRoomAccountAsync(Guid roomId, CreateRoomAccountRequest request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<RoomSessionDto>>> GetActiveSessionsAsync(Guid roomId, CancellationToken ct = default);
    Task<ServiceResult<RoomAccountDto>> ToggleRoomAccountActiveAsync(Guid roomAccountId, bool isActive, CancellationToken ct = default);
    Task<bool> ValidateRoomDeviceSessionAsync(Guid roomAccountId, string sessionHash, CancellationToken ct = default);
}
