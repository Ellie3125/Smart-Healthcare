using System.Security.Claims;
using IdentityPatients.Application.Auth;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[ApiController]
[Route("")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    private readonly IAuthService _authService = authService;

    [HttpPost("auth/staff/login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StaffLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginStaffAsync(request, ct);
        if (!result.IsSuccess)
        {
            return Problem(
                statusCode: result.StatusCode,
                title: result.ErrorTitle,
                detail: result.ErrorDetail);
        }

        return Ok(result.Response);
    }

    [HttpPost("auth/patient/login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PatientLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginPatientAsync(request, ct);
        if (!result.IsSuccess)
        {
            return Problem(
                statusCode: result.StatusCode,
                title: result.ErrorTitle,
                detail: result.ErrorDetail);
        }

        return Ok(result.Response);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var userName = User.Identity?.Name
                       ?? User.FindFirstValue(ClaimTypes.Name)
                       ?? string.Empty;

        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        Guid? doctorId = null;
        var doctorIdStr = User.FindFirstValue(AppClaims.DoctorId);
        if (!string.IsNullOrWhiteSpace(doctorIdStr) && Guid.TryParse(doctorIdStr, out var parsedDocId))
        {
            doctorId = parsedDocId;
        }

        Guid? roomId = null;
        var roomIdStr = User.FindFirstValue(AppClaims.RoomId);
        if (!string.IsNullOrWhiteSpace(roomIdStr) && Guid.TryParse(roomIdStr, out var parsedRoomId))
        {
            roomId = parsedRoomId;
        }

        return Ok(new CurrentUserDto(userId, userName, role, doctorId, roomId));
    }
}
