using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
}

