using IdentityPatients.Contracts;
using IdentityPatients.Application.Verifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[Authorize(Policy = Policies.ReceptionistOrAdmin)]
[Route("api/staff/verifications")]
public sealed class VerificationsController(IVerificationService verificationService) : ApiControllerBase
{
    private readonly IVerificationService _verificationService = verificationService;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VerificationItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchVerifications(
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new VerificationSearchQuery(searchTerm, status, page, pageSize);
        var result = await _verificationService.SearchVerificationsAsync(query, ct);
        return HandleResult(result);
    }

    [HttpGet("{representativeId:guid}")]
    [ProducesResponseType(typeof(VerificationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVerificationById(Guid representativeId, CancellationToken ct)
    {
        var result = await _verificationService.GetVerificationByIdAsync(representativeId, ct);
        return HandleResult(result);
    }

    [HttpPost("{representativeId:guid}/verify")]
    [ProducesResponseType(typeof(VerificationItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Verify(Guid representativeId, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId is null)
        {
            return Unauthorized();
        }

        var result = await _verificationService.VerifyAsync(staffUserId.Value, representativeId, ct);
        return HandleResult(result);
    }

    [HttpPost("{representativeId:guid}/reject")]
    [ProducesResponseType(typeof(VerificationItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid representativeId,
        [FromBody] RejectVerificationRequest request,
        CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId is null)
        {
            return Unauthorized();
        }

        var result = await _verificationService.RejectAsync(staffUserId.Value, representativeId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("{representativeId:guid}/revoke")]
    [ProducesResponseType(typeof(VerificationItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        Guid representativeId,
        [FromBody] RevokeVerificationRequest request,
        CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        if (staffUserId is null)
        {
            return Unauthorized();
        }

        var result = await _verificationService.RevokeAsync(staffUserId.Value, representativeId, request, ct);
        return HandleResult(result);
    }
}
