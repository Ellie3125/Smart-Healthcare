using IdentityPatients.Contracts;
using IdentityPatients.Application.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityPatients.Api.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
[Route("api/staff/admin")]
public sealed class StaffAdminController(IStaffAdminService adminService) : ApiControllerBase
{
    private readonly IStaffAdminService _adminService = adminService;

    // --- Specialties ---

    [HttpGet("specialties")]
    [ProducesResponseType(typeof(IReadOnlyList<SpecialtyAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpecialties(CancellationToken ct)
    {
        var result = await _adminService.GetSpecialtiesAsync(ct);
        return HandleResult(result);
    }

    [HttpPost("specialties")]
    [ProducesResponseType(typeof(SpecialtyAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSpecialty([FromBody] CreateSpecialtyRequest request, CancellationToken ct)
    {
        var result = await _adminService.CreateSpecialtyAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("specialties/{id:guid}")]
    [ProducesResponseType(typeof(SpecialtyAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSpecialty(Guid id, [FromBody] UpdateSpecialtyRequest request, CancellationToken ct)
    {
        var result = await _adminService.UpdateSpecialtyAsync(id, request, ct);
        return HandleResult(result);
    }

    // --- Doctor Ranks ---

    [HttpGet("doctor-ranks")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorRankAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctorRanks(CancellationToken ct)
    {
        var result = await _adminService.GetDoctorRanksAsync(ct);
        return HandleResult(result);
    }

    [HttpPost("doctor-ranks")]
    [ProducesResponseType(typeof(DoctorRankAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDoctorRank([FromBody] CreateDoctorRankRequest request, CancellationToken ct)
    {
        var result = await _adminService.CreateDoctorRankAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("doctor-ranks/{id:guid}")]
    [ProducesResponseType(typeof(DoctorRankAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDoctorRank(Guid id, [FromBody] UpdateDoctorRankRequest request, CancellationToken ct)
    {
        var result = await _adminService.UpdateDoctorRankAsync(id, request, ct);
        return HandleResult(result);
    }

    // --- Rooms ---

    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRooms(
        [FromQuery] string? roomType,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _adminService.GetRoomsAsync(roomType, isActive, ct);
        return HandleResult(result);
    }

    [HttpPost("rooms")]
    [ProducesResponseType(typeof(RoomAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var result = await _adminService.CreateRoomAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("rooms/{id:guid}")]
    [ProducesResponseType(typeof(RoomAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request, CancellationToken ct)
    {
        var result = await _adminService.UpdateRoomAsync(id, request, ct);
        return HandleResult(result);
    }

    // --- Staff ---

    [HttpGet("staff")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaffList(
        [FromQuery] string? staffType,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _adminService.GetStaffListAsync(staffType, isActive, ct);
        return HandleResult(result);
    }

    [HttpPost("staff")]
    [ProducesResponseType(typeof(StaffAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct)
    {
        var result = await _adminService.CreateStaffAsync(GetCurrentUserId() ?? Guid.Empty, request, ct);
        return HandleResult(result);
    }

    [HttpPut("staff/{id:guid}")]
    [ProducesResponseType(typeof(StaffAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffRequest request, CancellationToken ct)
    {
        var result = await _adminService.UpdateStaffAsync(GetCurrentUserId() ?? Guid.Empty, id, request, ct);
        return HandleResult(result);
    }

    [HttpPost("staff/{id:guid}/toggle-active")]
    [ProducesResponseType(typeof(StaffAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleStaffActive(Guid id, [FromQuery] bool isActive, CancellationToken ct)
    {
        var result = await _adminService.ToggleStaffActiveAsync(GetCurrentUserId() ?? Guid.Empty, id, isActive, ct);
        return HandleResult(result);
    }

    // --- Doctors ---

    [HttpGet("doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<DoctorAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(
        [FromQuery] Guid? specialtyId,
        [FromQuery] Guid? rankId,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var result = await _adminService.GetDoctorsAsync(specialtyId, rankId, isActive, ct);
        return HandleResult(result);
    }

    [HttpPost("doctors")]
    [ProducesResponseType(typeof(DoctorAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDoctor([FromBody] CreateDoctorRequest request, CancellationToken ct)
    {
        var result = await _adminService.CreateDoctorAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("doctors/{id:guid}")]
    [ProducesResponseType(typeof(DoctorAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDoctor(Guid id, [FromBody] UpdateDoctorRequest request, CancellationToken ct)
    {
        var result = await _adminService.UpdateDoctorAsync(id, request, ct);
        return HandleResult(result);
    }

    [HttpPost("doctors/{id:guid}/toggle-active")]
    [ProducesResponseType(typeof(DoctorAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleDoctorActive(Guid id, [FromQuery] bool isActive, CancellationToken ct)
    {
        var result = await _adminService.ToggleDoctorActiveAsync(id, isActive, ct);
        return HandleResult(result);
    }

    // --- Shift Assignments ---

    [HttpGet("shift-assignments")]
    [ProducesResponseType(typeof(IReadOnlyList<ShiftAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShiftAssignments(
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? roomId,
        [FromQuery] Guid? staffId,
        CancellationToken ct)
    {
        var result = await _adminService.GetShiftAssignmentsAsync(date, roomId, staffId, ct);
        return HandleResult(result);
    }

    [HttpPost("shift-assignments")]
    [ProducesResponseType(typeof(ShiftAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateShiftAssignment(
        [FromBody] CreateShiftAssignmentRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.CreateShiftAssignmentAsync(request, ct);
        return HandleResult(result);
    }

    [HttpDelete("shift-assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShiftAssignment(Guid id, CancellationToken ct)
    {
        var result = await _adminService.DeleteShiftAssignmentAsync(id, ct);
        return HandleResult(result);
    }
}
