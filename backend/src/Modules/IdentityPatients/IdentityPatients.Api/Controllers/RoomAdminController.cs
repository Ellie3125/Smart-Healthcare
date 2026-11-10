using IdentityPatients.Contracts;
using IdentityPatients.Application.Rooms;
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
    public async Task<IActionResult> GetActiveSessions(Guid roomId, CancellationToken ct)
    {
        var result = await _roomService.GetActiveSessionsAsync(roomId, ct);
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
}
