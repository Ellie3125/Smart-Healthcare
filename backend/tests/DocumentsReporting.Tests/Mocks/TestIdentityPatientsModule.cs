using IdentityPatients.Contracts;

namespace DocumentsReporting.Tests.Mocks;

public sealed class TestIdentityPatientsModule : IIdentityPatientsModule
{
    public Func<Guid, Guid, AccessLevel, Task<bool>>? CanAccessHandler { get; set; }

    public Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default)
    {
        if (CanAccessHandler != null)
            return CanAccessHandler(userId, patientId, level);

        return Task.FromResult(true);
    }

    public Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default) => Task.FromResult<DoctorDto?>(null);
    public Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DoctorDto>>([]);
    public Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SpecialtyDto>>([]);
    public Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DoctorRankDto>>([]);
    public Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default) => Task.FromResult<RoomDto?>(null);
    public Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RoomDto>>([]);
    public Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default) => Task.FromResult<StaffDutyLeaderDto?>(null);
}
