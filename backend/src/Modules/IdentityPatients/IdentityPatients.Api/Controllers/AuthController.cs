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
    public async Task<IActionResult> StaffLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginStaffAsync(request, ct);
        if (!result.IsSuccess)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, result.StatusCode, result.ErrorTitle, detail: result.ErrorDetail);
            problem.Extensions["code"] = result.ErrorCode;
            return new ObjectResult(problem) { StatusCode = result.StatusCode };
        }

        return Ok(result.Response);
    }

    [HttpPost("auth/patient/login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PatientLogin([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginPatientAsync(request, ct);
        if (!result.IsSuccess)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, result.StatusCode, result.ErrorTitle, detail: result.ErrorDetail);
            problem.Extensions["code"] = result.ErrorCode;
            return new ObjectResult(problem) { StatusCode = result.StatusCode };
        }

        return Ok(result.Response);
    }

    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("patient-register")]
    [HttpPost("auth/patient/register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> PatientRegister([FromBody] RegisterPatientRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterPatientAsync(request, ct);
        if (!result.IsSuccess)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, result.StatusCode, result.ErrorTitle, detail: result.ErrorDetail);
            problem.Extensions["code"] = result.ErrorCode;
            return new ObjectResult(problem) { StatusCode = result.StatusCode };
        }

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [Authorize(Policy = Policies.RoomAccountOnly)]
    [HttpPost("auth/room/logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RoomLogout(CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var rawSessionToken = User.FindFirstValue(ITokenService.RoomSessionClaim);
        var result = await _authService.LogoutRoomAsync(userId, rawSessionToken, ct);
        if (!result.IsSuccess)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, result.StatusCode, result.ErrorTitle, detail: result.ErrorDetail);
            problem.Extensions["code"] = result.ErrorCode;
            return new ObjectResult(problem) { StatusCode = result.StatusCode };
        }

        return Ok();
    }

    [Authorize(Policy = Policies.AnyChannel)]
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
