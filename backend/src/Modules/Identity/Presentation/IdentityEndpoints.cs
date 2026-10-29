using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartHealthcare.Identity.Application;
using SmartHealthcare.Identity.Contracts;
using SmartHealthcare.SharedKernel;

namespace SmartHealthcare.Identity.Presentation;

public sealed record RegisterRequest(string? UserName, string? Email, string? PhoneNumber, string? Password);

public sealed record RegisterResponse(Guid UserId);

public sealed record LoginRequest(string? Login, string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, Guid UserId, string Role);

public static class IdentityEndpoints
{
    /// <summary>Tên policy giới hạn tần suất cho đăng ký/đăng nhập; host đăng ký policy cùng tên.</summary>
    public const string AuthRateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/register", async (RegisterRequest body, RegisterAccount useCase, CancellationToken ct) =>
        {
            var command = new RegisterAccountCommand(body.UserName ?? "", body.Email ?? "", body.PhoneNumber ?? "", body.Password ?? "");
            var result = await useCase.ExecuteAsync(command, ct);
            return result.IsSuccess
                ? Results.Created("/api/auth/me", new RegisterResponse(result.Value))
                : ErrorResults.From(result.Error);
        }).AllowAnonymous().RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapPost("/login", async (LoginRequest body, LoginAccount useCase, CancellationToken ct) =>
        {
            var missing = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(body.Login)) missing["login"] = ["Nhập tên đăng nhập, email hoặc số điện thoại."];
            if (string.IsNullOrEmpty(body.Password)) missing["password"] = ["Nhập mật khẩu."];
            if (missing.Count > 0) return ErrorResults.From(Error.Validation(missing));

            var result = await useCase.ExecuteAsync(new LoginCommand(body.Login!, body.Password!), ct);
            return result.IsSuccess
                ? Results.Ok(new LoginResponse(result.Value.AccessToken, "Bearer", result.Value.ExpiresAtUtc, result.Value.UserId, result.Value.Role))
                : ErrorResults.From(result.Error);
        }).AllowAnonymous().RequireRateLimiting(AuthRateLimitPolicy);

        auth.MapGet("/me", async (HttpContext http, GetMyAccount useCase, CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var userId)) return Results.Unauthorized();
            var result = await useCase.ExecuteAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResults.From(result.Error);
        }).RequireAuthorization();

        return app;
    }
}
