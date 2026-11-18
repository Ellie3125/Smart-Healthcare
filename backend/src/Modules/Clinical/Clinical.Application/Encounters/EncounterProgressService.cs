using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Shared.Abstractions;

namespace Clinical.Application.Encounters;

public interface IEncounterProgressStore
{
    /// <summary>Khóa Encounter (UPDLOCK) trong transaction của bên gọi, trả bản đang theo dõi để cập nhật.</summary>
    Task<Encounter?> LockAsync(Guid encounterId, CancellationToken ct);
    Task<bool> IsRecordPublishedAsync(Guid encounterId, CancellationToken ct);
    Task SaveAsync(Encounter encounter, EncounterStatus from, Guid performedByUserId, DateTime nowUtc, CancellationToken ct);
}

/// <summary>
/// Tiến trình lượt khám theo thao tác của bác sĩ trên hàng đợi (trưởng nhóm chốt 18/11/2026). Scheduling gọi trong cùng
/// IUnitOfWork, sau khi đổi trạng thái vé; lỗi thì rollback cả vé.
/// - Bắt đầu vé quay lại: WaitingCLS → ReturningDoctor (bác sĩ đọc kết quả, có thể chỉ định đợt mới).
/// - Khám xong vé khám đầu/quay lại: còn chờ CLS (WaitingCLS) thì giữ nguyên; ngược lại bệnh án phải đã phát hành, Encounter → Completed.
/// </summary>
public sealed class EncounterProgressService(IEncounterProgressStore store, IDbClock clock)
{
    public async Task StartReturnVisitAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct)
    {
        var encounter = await LockAsync(encounterId, performedByUserId, ct);
        if (encounter.Status != EncounterStatus.WaitingCLS)
            return; // đã ReturningDoctor (gửi lại lệnh) hoặc trạng thái khác: không đổi
        await store.SaveAsync(Move(encounter, EncounterStatus.ReturningDoctor), EncounterStatus.WaitingCLS, performedByUserId,
            await clock.GetUtcNowAsync(ct), ct);
    }

    public async Task FinishConsultationAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct)
    {
        var encounter = await LockAsync(encounterId, performedByUserId, ct);
        if (encounter.Status is EncounterStatus.Completed or EncounterStatus.WaitingCLS)
            return; // đã kết thúc, hoặc bệnh nhân đang sang phòng CLS
        if (!await store.IsRecordPublishedAsync(encounterId, ct))
            throw new ClinicalException(ClinicalErrorCode.RecordNotPublished, "Bác sĩ cần phát hành bệnh án trước khi kết thúc lượt khám.");
        var from = encounter.Status;
        var now = await clock.GetUtcNowAsync(ct);
        Move(encounter, EncounterStatus.Completed).CompletedAtUtc = now;
        await store.SaveAsync(encounter, from, performedByUserId, now, ct);
    }

    private async Task<Encounter> LockAsync(Guid encounterId, Guid performedByUserId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty || performedByUserId == Guid.Empty)
            throw new ClinicalException(ClinicalErrorCode.InvalidRequest, "Lệnh tiến trình lượt khám không hợp lệ.");
        var encounter = await store.LockAsync(encounterId, ct)
            ?? throw new ClinicalException(ClinicalErrorCode.EncounterNotFound, "Không tìm thấy lượt khám.");
        if (encounter.Status == EncounterStatus.Abandoned)
            throw new ClinicalException(ClinicalErrorCode.EncounterClosed, "Lượt khám đã kết thúc.");
        return encounter;
    }

    private static Encounter Move(Encounter encounter, EncounterStatus to)
    {
        encounter.Status = to;
        return encounter;
    }
}
