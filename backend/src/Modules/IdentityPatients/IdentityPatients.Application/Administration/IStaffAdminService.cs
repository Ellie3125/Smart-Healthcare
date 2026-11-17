using IdentityPatients.Application.Common;

namespace IdentityPatients.Application.Administration;

public sealed record CreateSpecialtyRequest(string Code, string Name, string? Description);
public sealed record UpdateSpecialtyRequest(string Name, string? Description, bool IsActive);
public sealed record SpecialtyAdminDto(Guid Id, string Code, string Name, string? Description, bool IsActive);

public sealed record CreateDoctorRankRequest(string Code, string Name, int DisplayOrder);
public sealed record UpdateDoctorRankRequest(string Name, int DisplayOrder, bool IsActive);
public sealed record DoctorRankAdminDto(Guid Id, string Code, string Name, int DisplayOrder, bool IsActive);

public sealed record CreateRoomRequest(string RoomNumber, string RoomName, string RoomType, string DepartmentName);
public sealed record UpdateRoomRequest(string RoomName, string RoomType, string DepartmentName, bool IsActive);
public sealed record RoomAdminDto(Guid Id, string RoomNumber, string RoomName, string RoomType, string DepartmentName, bool IsActive);

public sealed record CreateStaffRequest(string UserName, string Email, string PhoneNumber, string Password, string StaffCode, string FullName, string StaffType);
public sealed record UpdateStaffRequest(string FullName, bool IsActive);
public sealed record ResetStaffPasswordRequest(string NewPassword);
public sealed record StaffAdminDto(Guid Id, Guid AppUserId, string StaffCode, string FullName, string StaffType, string UserName, string Email, string? PhoneNumber, bool IsActive, DateTime CreatedAtUtc);

public sealed record CreateDoctorRequest(Guid StaffId, Guid SpecialtyId, Guid DoctorRankId, string? Biography);
public sealed record UpdateDoctorRequest(Guid SpecialtyId, Guid DoctorRankId, string? Biography, bool IsActive);
public sealed record DoctorAdminDto(Guid Id, Guid StaffId, string FullName, string StaffCode, Guid SpecialtyId, string SpecialtyName, Guid DoctorRankId, string RankName, string? Biography, bool IsActive);

public sealed record CreateShiftAssignmentRequest(Guid StaffId, Guid RoomId, DateOnly ShiftDate, string ShiftName, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsShiftLeader);
public sealed record ShiftAssignmentDto(Guid Id, Guid StaffId, string StaffName, string StaffCode, Guid RoomId, string RoomNumber, string RoomName, DateOnly ShiftDate, string ShiftName, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsShiftLeader);

public interface IStaffAdminService
{
    // Specialties
    Task<ServiceResult<IReadOnlyList<SpecialtyAdminDto>>> GetSpecialtiesAsync(CancellationToken ct = default);
    Task<ServiceResult<SpecialtyAdminDto>> CreateSpecialtyAsync(CreateSpecialtyRequest request, CancellationToken ct = default);
    Task<ServiceResult<SpecialtyAdminDto>> UpdateSpecialtyAsync(Guid id, UpdateSpecialtyRequest request, CancellationToken ct = default);

    // Doctor Ranks
    Task<ServiceResult<IReadOnlyList<DoctorRankAdminDto>>> GetDoctorRanksAsync(CancellationToken ct = default);
    Task<ServiceResult<DoctorRankAdminDto>> CreateDoctorRankAsync(CreateDoctorRankRequest request, CancellationToken ct = default);
    Task<ServiceResult<DoctorRankAdminDto>> UpdateDoctorRankAsync(Guid id, UpdateDoctorRankRequest request, CancellationToken ct = default);

    // Rooms
    Task<ServiceResult<IReadOnlyList<RoomAdminDto>>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default);
    Task<ServiceResult<RoomAdminDto>> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<ServiceResult<RoomAdminDto>> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default);

    // Staff
    Task<ServiceResult<IReadOnlyList<StaffAdminDto>>> GetStaffListAsync(string? staffType = null, bool? isActive = null, string? searchTerm = null, CancellationToken ct = default);
    Task<ServiceResult<StaffAdminDto>> CreateStaffAsync(Guid actorUserId, CreateStaffRequest request, CancellationToken ct = default);
    Task<ServiceResult<StaffAdminDto>> UpdateStaffAsync(Guid actorUserId, Guid id, UpdateStaffRequest request, CancellationToken ct = default);
    Task<ServiceResult<StaffAdminDto>> ToggleStaffActiveAsync(Guid actorUserId, Guid id, bool isActive, CancellationToken ct = default);
    Task<ServiceResult> ResetStaffPasswordAsync(Guid actorUserId, Guid staffId, ResetStaffPasswordRequest request, CancellationToken ct = default);

    // Doctor
    Task<ServiceResult<IReadOnlyList<DoctorAdminDto>>> GetDoctorsAsync(Guid? specialtyId = null, Guid? rankId = null, bool? isActive = null, CancellationToken ct = default);
    Task<ServiceResult<DoctorAdminDto>> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct = default);
    Task<ServiceResult<DoctorAdminDto>> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken ct = default);
    Task<ServiceResult<DoctorAdminDto>> ToggleDoctorActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    // Shifts
    Task<ServiceResult<IReadOnlyList<ShiftAssignmentDto>>> GetShiftAssignmentsAsync(DateOnly? date = null, Guid? roomId = null, Guid? staffId = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    Task<ServiceResult<ShiftAssignmentDto>> CreateShiftAssignmentAsync(CreateShiftAssignmentRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteShiftAssignmentAsync(Guid id, CancellationToken ct = default);
}
