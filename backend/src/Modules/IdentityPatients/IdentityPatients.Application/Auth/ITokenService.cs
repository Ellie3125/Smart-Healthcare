using IdentityPatients.Contracts.Dtos;
using IdentityPatients.Domain.Entities;

namespace IdentityPatients.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
    CurrentUserDto GetCurrentUserFromClaims(System.Security.Claims.ClaimsPrincipal principal);
}

