namespace Clinical.Contracts;

/// <summary>
/// Public contract cho module Clinical (GP §8.2.1, CLA-FIX-013).
/// Định nghĩa tối thiểu phục vụ kiểm quyền truy cập tệp kết quả CLS và lượt khám (Tuần 3).
/// </summary>
public interface IClinicalModule
{
    /// <summary>
    /// Kiểm tra bác sĩ có phải là bác sĩ khám ban đầu của lượt khám hay không.
    /// Bác sĩ ban đầu có quyền xem kết quả CLS ngay khi kết quả được Published (GP §6.5).
    /// </summary>
    Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra bác sĩ khám ban đầu đã phát hành kết luận sau CLS (PostCLSConclusion) hay chưa.
    /// Bệnh nhân và người đại diện chỉ được xem kết quả và tệp sau khi bước này hoàn tất (GP §6.5).
    /// </summary>
    Task<bool> IsPostClsConclusionPublishedAsync(Guid encounterId, CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin tóm tắt của kết quả CLS.
    /// </summary>
    Task<ClsResultInfoDto?> GetClsResultAsync(Guid clsResultId, CancellationToken ct = default);
}
