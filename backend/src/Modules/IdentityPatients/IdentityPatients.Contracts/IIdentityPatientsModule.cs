using IdentityPatients.Contracts.Dtos;

namespace IdentityPatients.Contracts;

public interface IIdentityPatientsModule
{
    /// <summary>
    /// Kiểm tra quyền truy cập hồ sơ bệnh nhân theo AppUserId và PatientId.
    /// Nhân viên y tế (Doctor, Nurse, Receptionist): true.
    /// Patient: true nếu là chính chủ (hoặc có quan hệ đại diện Verified).
    /// Admin kỹ thuật & RoomAccount: false.
    /// </summary>
    Task<bool> CanAccessPatientAsync(Guid userId, Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin chi tiết bác sĩ theo DoctorId.
    /// </summary>
    Task<DoctorDto?> GetDoctorAsync(Guid doctorId, CancellationToken ct = default);

    /// <summary>
    /// Tìm kiếm và lọc danh sách bác sĩ theo SpecialtyId, RankId hoặc từ khóa.
    /// </summary>
    Task<IReadOnlyList<DoctorDto>> SearchDoctorsAsync(DoctorSearch query, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh mục chuyên khoa (Specialty.Code là cố định, phục vụ tra cứu và chatbot).
    /// </summary>
    Task<IReadOnlyList<SpecialtyDto>> GetSpecialtiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy danh mục cấp bậc bác sĩ (phục vụ hiển thị và làm căn cứ áp giá ở Billing).
    /// </summary>
    Task<IReadOnlyList<DoctorRankDto>> GetDoctorRanksAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin phòng khám/phòng CLS theo RoomId.
    /// </summary>
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
}

