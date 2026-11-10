using IdentityPatients.Application.Patients;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[Authorize(Policy = "PatientOnly")]
[Route("api/patient/profiles")]
public sealed class PatientProfilesController(IPatientProfileService profileService) : ApiControllerBase
{
    private readonly IPatientProfileService _profileService = profileService;

    [HttpPost]
    [ProducesResponseType(typeof(PatientProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile([FromBody] CreatePatientProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.CreateProfileAsync(userId.Value, request, ct);
        return HandleResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserProfiles(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetUserProfilesAsync(userId.Value, ct);
        return HandleResult(result);
    }

    [HttpGet("{patientId:guid}")]
    [ProducesResponseType(typeof(PatientProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileById(Guid patientId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetProfileByIdAsync(userId.Value, patientId, ct);
        return HandleResult(result);
    }

    [HttpPut("{patientId:guid}")]
    [ProducesResponseType(typeof(PatientProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(
        Guid patientId,
        [FromBody] UpdatePatientProfileRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateProfileAsync(userId.Value, patientId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("{patientId:guid}/unlink")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkProfile(Guid patientId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UnlinkProfileAsync(userId.Value, patientId, ct);
        return HandleResult(result);
    }
}
