using IdentityPatients.Contracts.Dtos;

namespace IdentityPatients.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record AuthResponse(
    string AccessToken,
    int ExpiresInSeconds,
    CurrentUserDto User
);

