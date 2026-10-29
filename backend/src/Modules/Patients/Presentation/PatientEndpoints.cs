using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmartHealthcare.Identity.Contracts;
using SmartHealthcare.Patients.Application;

namespace SmartHealthcare.Patients.Presentation;

public sealed record CreatePatientRequest(
    string? Relationship,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? NationalId,
    string? PhoneNumber,
    string? Address);

public sealed record UpdatePatientRequest(
    string? Version,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? NationalId,
    string? PhoneNumber,
    string? Address);

public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var patients = app.MapGroup("/api/patients").WithTags("Patients")
            .RequireAuthorization(AuthenticatedUser.PatientAccountPolicy);

        patients.MapGet("/", async (HttpContext http, ListMyPatientProfiles useCase, CancellationToken ct) =>
            http.User.TryGetUserId(out var userId)
                ? Results.Ok(await useCase.ExecuteAsync(userId, ct))
                : Results.Unauthorized());

        patients.MapPost("/", async (CreatePatientRequest body, HttpContext http, CreatePatientProfile useCase, CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var userId)) return Results.Unauthorized();
            var result = await useCase.ExecuteAsync(
                new CreatePatientProfileCommand(userId, body.Relationship, body.FullName, body.DateOfBirth, body.Gender, body.NationalId, body.PhoneNumber, body.Address),
                ct);
            return result.IsSuccess
                ? Results.Created($"/api/patients/{result.Value.Id}", result.Value)
                : ErrorResults.From(result.Error);
        });

        patients.MapGet("/{patientId:guid}", async (Guid patientId, HttpContext http, GetPatientProfile useCase, CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var userId)) return Results.Unauthorized();
            var result = await useCase.ExecuteAsync(userId, patientId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResults.From(result.Error);
        });

        patients.MapPut("/{patientId:guid}", async (Guid patientId, UpdatePatientRequest body, HttpContext http, UpdatePatientProfile useCase, CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var userId)) return Results.Unauthorized();
            var result = await useCase.ExecuteAsync(
                new UpdatePatientProfileCommand(userId, patientId, body.Version, body.FullName, body.DateOfBirth, body.Gender, body.NationalId, body.PhoneNumber, body.Address),
                ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResults.From(result.Error);
        });

        patients.MapDelete("/{patientId:guid}/my-link", async (Guid patientId, HttpContext http, RevokeMyProfileLink useCase, CancellationToken ct) =>
        {
            if (!http.User.TryGetUserId(out var userId)) return Results.Unauthorized();
            var result = await useCase.ExecuteAsync(userId, patientId, ct);
            return result.IsSuccess ? Results.NoContent() : ErrorResults.From(result.Error);
        });

        return app;
    }
}
