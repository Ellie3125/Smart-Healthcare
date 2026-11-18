namespace Clinical.Contracts;

/// <summary>
/// Public Contract của module Clinical được công bố cho các module khác trong hệ thống.
/// Module khác chỉ tham chiếu project Clinical.Contracts để gọi In-Process.
/// </summary>
public interface IClinicalModule
{
    /// <summary>Read-only diagnoses of the current Published clinical record, in diagnosis time/ID order; Draft or missing record returns empty.</summary>
    Task<IReadOnlyList<EncounterDiagnosisDto>> GetPublishedDiagnosesAsync(Guid encounterId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    /// <summary>Read-only encounter check-ins grouped in GMT+7; caller validates UTC interval [from,until), at most31 days.</summary>
    Task<IReadOnlyList<ClinicalDailyReport>> GetDailyReportAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct = default) =>
        throw new NotSupportedException();

    /// <summary>Scheduling calls after its paid-ticket transition to InProgress, inside the caller's IUnitOfWork.
    /// Verifies ticket room/item and an open encounter; Ordered → InProgress, replay InProgress is a no-op.
    /// Errors: InvalidRequest, OrderItemNotFound, InvalidStatusTransition, EncounterClosed, Forbidden. Never commits the caller's transaction.</summary>
    Task StartClsServiceAsync(ClsServiceCommand command, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Scheduling calls after ticket Finish, in the same IUnitOfWork. InProgress → ServiceFinished and creates a Draft result.
    /// Replay ServiceFinished/Completed is a no-op; errors as Start. A caller failure rolls back both Clinical and Scheduling.</summary>
    Task FinishClsServiceAsync(ClsServiceCommand command, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Scheduling gọi khi bác sĩ bắt đầu vé quay lại, trong cùng IUnitOfWork: WaitingCLS → ReturningDoctor.</summary>
    Task StartReturnVisitAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Scheduling gọi khi bác sĩ khám xong vé khám đầu/quay lại, trong cùng IUnitOfWork. Còn chờ CLS thì không đổi;
    /// ngược lại bệnh án phải đã phát hành (RecordNotPublished), Encounter → Completed.</summary>
    Task FinishConsultationAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct) => throw new NotSupportedException();

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

    /// <summary>Bác sĩ (DoctorId) có phải bác sĩ khám ban đầu của lượt khám; dùng để cho xem kết quả CLS ngay khi Published (GP §6.5).</summary>
    Task<bool> IsOriginalDoctorAsync(Guid doctorId, Guid encounterId, CancellationToken ct);

    /// <summary>Bác sĩ ban đầu đã phát hành kết luận sau CLS; trước đó bệnh nhân và người đại diện chưa xem được kết quả (GP §6.5).</summary>
    Task<bool> IsPostClsConclusionPublishedAsync(Guid encounterId, CancellationToken ct);

    /// <summary>Caller has authorized the staff room queue; at most100 encounter IDs, one batch, no medical content.</summary>
    Task<IReadOnlyList<QueuePatientContext>> GetQueuePatientContextsAsync(IReadOnlyCollection<Guid> encounterIds, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

