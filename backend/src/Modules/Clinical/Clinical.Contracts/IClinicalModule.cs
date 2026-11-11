namespace Clinical.Contracts;

/// <summary>
/// Public Contract của module Clinical được công bố cho các module khác trong hệ thống.
/// Module khác chỉ tham chiếu project Clinical.Contracts để gọi In-Process.
/// </summary>
public interface IClinicalModule
{
    /// <summary>Lấy thông tin chi tiết của một lượt khám theo EncounterId.</summary>
    Task<EncounterDto?> GetEncounterByIdAsync(Guid encounterId, CancellationToken ct);

    /// <summary>Khởi tạo lượt khám mới khi bệnh nhân check-in tại phòng khám.</summary>
    Task<EncounterDto> StartEncounterAsync(StartEncounterCommand command, CancellationToken ct);

    /// <summary>Kiểm tra bệnh nhân có đang có lượt khám nào dở dang (chưa kết thúc hoặc chưa hủy) hay không.</summary>
    Task<bool> HasActiveEncounterAsync(Guid patientId, CancellationToken ct);

    /// <summary>Lấy danh sách tóm tắt lịch sử các lượt khám của một bệnh nhân.</summary>
    Task<IReadOnlyList<EncounterSummaryDto>> GetPatientEncountersAsync(Guid patientId, CancellationToken ct);

    /// <summary>Lấy hồ sơ bệnh án lâm sàng của lượt khám.</summary>
    Task<EncounterClinicalRecordDto?> GetClinicalRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    /// <summary>Lấy danh sách tất cả các phiếu chỉ định cận lâm sàng của lượt khám.</summary>
    Task<IReadOnlyList<ServiceOrderDto>> GetServiceOrdersByEncounterIdAsync(Guid encounterId, CancellationToken ct);

    /// <summary>Lấy danh sách tất cả đơn thuốc của lượt khám.</summary>
    Task<IReadOnlyList<PrescriptionDto>> GetPrescriptionsByEncounterIdAsync(Guid encounterId, CancellationToken ct);
}

