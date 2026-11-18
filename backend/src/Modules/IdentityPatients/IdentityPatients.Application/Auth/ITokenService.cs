using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Application.Auth;

public interface ITokenService
{
    public const string RoomSessionClaim = "room_session";

    string GenerateAccessToken(AppUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null);

    string GenerateAccessToken(
        AppUser user,
        string role,
        string channel,
        Guid? doctorId,
        Guid? roomId,
        string? roomSessionToken) =>
        GenerateAccessToken(user, role, channel, doctorId, roomId);
}
