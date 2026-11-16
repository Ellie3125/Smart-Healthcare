using Billing.Contracts;
using IdentityPatients.Contracts;

namespace Billing.Application;

/// <summary>
/// Tổng hợp chi phí một lượt khám cho web bệnh nhân (Phạm vi nghiệp vụ §5.5): phí khám đã gắn lượt khám và phí CLS.
/// Chỉ người có quyền trên hồ sơ bệnh nhân mới xem được; người khác nhận kết quả như không tồn tại.
/// </summary>
public sealed class PatientCostSummaryService(IChargeStore charges, IIdentityPatientsModule identity)
{
    /// <returns>null khi lượt khám không có khoản thu nào, hoặc người gọi không có quyền.</returns>
    public async Task<EncounterCostSummary?> GetAsync(Guid encounterId, Guid userId, CancellationToken ct)
    {
        if (encounterId == Guid.Empty)
            return null;

        var items = await charges.GetByEncounterAsync(encounterId, ct);
        // Khoản trước khi Billing lưu PatientId (dữ liệu cũ) không xác định được chủ; không đoán.
        var patients = items.Select(i => i.PatientId).Distinct().ToList();
        if (patients is not [{ } patientId])
            return null;

        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct))
            return null;

        return CostSummaryCalculator.Summarize(encounterId, items);
    }
}
