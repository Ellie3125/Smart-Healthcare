using IdentityPatients.Application.Rooms;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
[Route("api/staff/admin/rooms")]
public sealed class RoomAdminController(IRoomManagementService roomService) : ApiControllerBase
{
    private readonly IRoomManagementService _roomService = roomService;

    [HttpGet("{roomId:guid}/sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessions(
        Guid roomId,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var result = await _roomService.GetSessionsByRoomAsync(roomId, activeOnly, ct);
        return HandleResult(result);
    }

    [HttpGet("{roomId:guid}/account")]
    [ProducesResponseType(typeof(RoomAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomAccount(Guid roomId, CancellationToken ct)
    {
        var result = await _roomService.GetRoomAccountByRoomIdAsync(roomId, ct);
        return HandleResult(result);
    }

    [HttpPost("{roomId:guid}/account")]
    [ProducesResponseType(typeof(RoomAccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRoomAccount(
        Guid roomId,
        [FromBody] CreateRoomAccountRequest request,
        CancellationToken ct)
    {
        var result = await _roomService.CreateRoomAccountAsync(roomId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("accounts/{roomAccountId:guid}/toggle-active")]
    [ProducesResponseType(typeof(RoomAccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleRoomAccountActive(
        Guid roomAccountId,
        [FromQuery] bool isActive,
        CancellationToken ct)
    {
        var result = await _roomService.ToggleRoomAccountActiveAsync(roomAccountId, isActive, ct);
        return HandleResult(result);
    }

    [HttpPost("accounts/{roomAccountId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetRoomAccountPassword(
        Guid roomAccountId,
        [FromBody] ResetRoomPasswordRequest request,
        CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId() ?? Guid.Empty;
        var result = await _roomService.ResetRoomAccountPasswordAsync(actorUserId, roomAccountId, request, ct);
        return HandleResult(result);
    }

    [HttpPost("sessions/{sessionId:guid}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        [FromBody] RevokeSessionRequest? request,
        CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId() ?? Guid.Empty;
        var reason = request?.Reason ?? "Admin buộc đăng xuất phiên thiết bị phòng";
        var result = await _roomService.RevokeSessionAsync(actorUserId, sessionId, reason, ct);
        return HandleResult(result);
    }
}
