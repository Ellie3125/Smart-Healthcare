using Billing.Domain.Entities;

namespace Billing.Application;

/// <summary>Một dòng bảng giá kèm bảng giá chứa nó.</summary>
public sealed record PriceCandidate(PriceList PriceList, PriceListItem Item);

/// <summary>
/// Đọc bảng giá. Được phép lọc thô ở DB (đang bật, đúng loại tiền, đúng căn cứ); PriceResolver vẫn kiểm lại
/// hiệu lực theo thời điểm và tính duy nhất.
/// </summary>
public interface IPriceCatalog
{
    Task<IReadOnlyList<PriceCandidate>> GetConsultationCandidatesAsync(Guid doctorId, Guid doctorRankId, DateTime atUtc, CancellationToken ct);

    Task<IReadOnlyList<PriceCandidate>> GetClsCandidatesAsync(IReadOnlyCollection<Guid> medicalServiceIds, DateTime atUtc, CancellationToken ct);
}

/// <summary>Lưu và đọc khoản thu (billing.BillingItem).</summary>
public interface IChargeStore
{
    Task<IReadOnlyList<BillingItem>> GetByServiceOrderItemsAsync(IReadOnlyCollection<Guid> serviceOrderItemIds, CancellationToken ct);

    Task<IReadOnlyList<BillingItem>> GetByEncounterAsync(Guid encounterId, CancellationToken ct);

    /// <summary>Khoản CLS của một đợt chỉ định (không khóa).</summary>
    Task<IReadOnlyList<BillingItem>> GetClsByServiceOrderAsync(Guid serviceOrderId, CancellationToken ct);

    /// <summary>Khoản CLS của bệnh nhân tạo từ <paramref name="fromUtc"/> trở đi (không khóa).</summary>
    Task<IReadOnlyList<BillingItem>> GetClsByPatientAsync(Guid patientId, DateTime fromUtc, CancellationToken ct);

    /// <summary>Id các khoản CLS còn Pending tạo trước <paramref name="beforeUtc"/>, tối đa <paramref name="take"/>, theo Id (không khóa).</summary>
    Task<IReadOnlyList<Guid>> FindUnpaidClsBeforeAsync(DateTime beforeUtc, int take, CancellationToken ct);

    /// <summary>
    /// Id phí khám đã thu cho một lượt đặt lịch: khoản ConsultationFee được phân bổ trong yêu cầu thanh toán Succeeded của
    /// <paramref name="bookingAttemptId"/>, cộng các khoản đang gắn <paramref name="appointmentId"/> (lịch cũ khi đổi lịch).
    /// </summary>
    Task<IReadOnlyList<Guid>> FindExamChargeIdsAsync(Guid bookingAttemptId, Guid? appointmentId, CancellationToken ct);

    /// <summary>Id phí khám đang gắn lịch hẹn <paramref name="appointmentId"/>.</summary>
    Task<IReadOnlyList<Guid>> FindExamChargeIdsByAppointmentAsync(Guid appointmentId, CancellationToken ct);

    void Add(BillingItem item);

    Task SaveChangesAsync(CancellationToken ct);
}
