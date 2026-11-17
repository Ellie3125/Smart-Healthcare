namespace IdentityPatients.Contracts;

public enum AccessLevel
{
    Booking,
    ClinicalRecords
}

public interface IIdentityPatientsModule
{
    Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, AccessLevel level = AccessLevel.Booking, CancellationToken ct = default);

    /// <summary>
    /// Các PatientId trong <paramref name="patientIds"/> mà <paramref name="userId"/> được truy cập ở mức <paramref name="level"/>
    /// (cùng luật <see cref="CanAccessPatientAsync"/>), cho danh sách nhiều hồ sơ trong một request. Bản cài đặt Identity
    /// dùng một truy vấn; bản mặc định gọi lần lượt để các bản giả trong test không phải cài lại.
    /// </summary>
    async Task<IReadOnlySet<Guid>> GetAccessiblePatientsAsync(Guid userId, IReadOnlyCollection<Guid> patientIds,
        AccessLevel level = AccessLevel.Booking, CancellationToken ct = default)
    {
        var allowed = new HashSet<Guid>();
        foreach (var patientId in patientIds.Distinct())
        {
            if (await CanAccessPatientAsync(userId, patientId, level, ct))
                allowed.Add(patientId);
        }

        return allowed;
    }
    Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default);
    Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<IReadOnlyList<RoomDto>> GetRoomsAsync(string? roomType = null, bool? isActive = null, CancellationToken ct = default);
    Task<StaffDutyLeaderDto?> GetRoomShiftLeaderAsync(Guid roomId, DateOnly date, string shiftName, CancellationToken ct = default);
}
