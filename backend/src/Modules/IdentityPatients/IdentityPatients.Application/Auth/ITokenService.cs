using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, string role, string channel, Guid? doctorId = null, Guid? roomId = null);
}

