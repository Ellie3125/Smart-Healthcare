using IdentityPatients.Application.Patients;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[Authorize(Policy = Policies.ReceptionistOrAdmin)]
[Route("api/staff/patients")]
public sealed class StaffPatientsController(IPatientProfileService profileService) : ApiControllerBase
{
    private readonly IPatientProfileService _profileService = profileService;

    [HttpPut("{patientId:guid}")]
    [ProducesResponseType(typeof(StaffPatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePatientProfile(
        Guid patientId,
        [FromBody] StaffUpdatePatientProfileRequest request,
        CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateProfileByStaffAsync(staffUserId.Value, patientId, request, ct);
        return HandleResult(result);
    }
}
