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

/// <summary>Một phiếu thu tiền mặt tại quầy.</summary>
public sealed record CounterReceipt(
    string ReceiptNumber,
    Guid PaymentIntentId,
    Guid PatientId,
    Guid? EncounterId,
    decimal Amount,
    DateTime CollectedAtUtc);

/// <summary>Ghi vết và đọc phiếu thu tiền mặt tại quầy.</summary>
public interface ICashierStore
{
    Task<CashierShiftClosure?> FindClosureAsync(Guid cashierUserId, DateOnly date, CancellationToken ct) => throw new NotSupportedException();
    Task<CashierShiftClosure?> LockClosureAsync(Guid cashierUserId, DateOnly date, CancellationToken ct) => throw new NotSupportedException();
    Task EnsureShiftOpenAsync(Guid cashierUserId, DateOnly date, CancellationToken ct) => throw new NotSupportedException();
    Task SaveClosureAsync(CashierShiftClosure closure, string? clientIp, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>
    /// Ghi shared.AuditLog cho phiếu thu trong cùng transaction với thay đổi của Billing (Giải pháp kỹ thuật v2.13 mục 5.5).
    /// Gọi bên trong <see cref="IPaymentStore.InTransactionAsync{T}"/>.
    /// </summary>
    Task WriteCollectionAuditAsync(Guid staffUserId, Guid paymentTransactionId, string newValuesJson, string? clientIp, CancellationToken ct,
        DateTime? timestampUtc = null);

    /// <summary>Phiếu thu tiền mặt đã ghi nhận do <paramref name="staffUserId"/> thu trong [fromUtc, toUtc), theo thời điểm thu.</summary>
    Task<IReadOnlyList<CounterReceipt>> GetReceiptsAsync(Guid staffUserId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Phiếu thu tiền mặt phí khám đã ghi cho lượt đặt lịch và khoản phí khám này; null khi chưa thu.</summary>
    Task<CounterReceipt?> FindExamReceiptAsync(Guid bookingAttemptId, Guid chargeId, CancellationToken ct);

    /// <summary>Phiếu hoàn tiền mặt do <paramref name="staffUserId"/> chi trong [fromUtc, toUtc), theo thời điểm chi.</summary>
    Task<IReadOnlyList<CashRefundSlip>> GetCashRefundsAsync(Guid staffUserId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
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

    /// <summary>Khoản CLS tạo từ <paramref name="fromUtc"/> trở đi của mọi bệnh nhân, tối đa <paramref name="take"/> (không khóa).</summary>
    Task<IReadOnlyList<BillingItem>> GetClsSinceAsync(DateTime fromUtc, int take, CancellationToken ct);

    /// <summary>Id phí khám đang gắn lịch hẹn <paramref name="appointmentId"/>.</summary>
    Task<IReadOnlyList<Guid>> FindExamChargeIdsByAppointmentAsync(Guid appointmentId, CancellationToken ct);

    void Add(BillingItem item);

    Task SaveChangesAsync(CancellationToken ct);
}
