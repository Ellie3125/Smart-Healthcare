namespace IdentityPatients.Application.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResponse(
    string AccessToken,
    string Role,
    string UserName,
    Guid UserId,
    Guid? DoctorId,
    Guid? RoomId);

public sealed record CurrentUserDto(
    Guid UserId,
    string UserName,
    string Role,
    Guid? DoctorId,
    Guid? RoomId);

public sealed class AuthResult
{
    public bool IsSuccess { get; private init; }
    public LoginResponse? Response { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorDetail { get; private init; }

    public static AuthResult Success(LoginResponse response) =>
        new() { IsSuccess = true, Response = response, StatusCode = 200 };

    public static AuthResult Failure(int statusCode, string title, string detail) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorTitle = title, ErrorDetail = detail };
}

public interface IAuthService
{
    Task<AuthResult> LoginStaffAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginPatientAsync(LoginRequest request, CancellationToken ct = default);
}

