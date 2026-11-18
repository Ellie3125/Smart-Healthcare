using Billing.Contracts;
using Billing.Domain.Entities;
using IdentityPatients.Contracts;

namespace Billing.Application;

public enum ClsBatchStatus
{
    /// <summary>Còn khoản phải trả và chưa hết ngày khám.</summary>
    PendingPayment,

    /// <summary>Mọi khoản còn hiệu lực đã trả đủ.</summary>
    Paid,

    /// <summary>Hết ngày khám mà còn khoản chưa trả; job cuối ngày sẽ hủy các khoản này (Giải pháp kỹ thuật v2.13 mục 5.5).</summary>
    Expired,

    /// <summary>Mọi khoản đã hủy hoặc được miễn.</summary>
    Cancelled,
}

/// <param name="Outstanding">Phần còn phải trả của khoản; 0 với khoản đã trả đủ, đã hủy hoặc được miễn.</param>
public sealed record PatientClsBatchItem(
    Guid ChargeId,
    Guid? ServiceOrderItemId,
    string ServiceName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalAmount,
    decimal Outstanding,
    ChargeStatus Status);

/// <summary>Một đợt chỉ định CLS nhìn từ phía thanh toán. Tên phòng, bác sĩ, mã lịch hẹn thuộc Clinical và Scheduling.</summary>
/// <param name="OrderedAtUtc">Thời điểm Billing tạo khoản đầu tiên của đợt (lúc bác sĩ chỉ định).</param>
/// <param name="PayByUtc">Hạn cuối: hết ngày khám theo giờ Việt Nam.</param>
/// <param name="PaidAmount">Đã trả (đã xác minh) trừ đã hoàn, trên các khoản còn hiệu lực.</param>
/// <param name="CanPay">Còn tạo được yêu cầu thanh toán online cho đợt này.</param>
public sealed record PatientClsBatch(
    Guid ServiceOrderId,
    Guid EncounterId,
    Guid PatientId,
    DateTime OrderedAtUtc,
    DateTime PayByUtc,
    ClsBatchStatus Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Outstanding,
    string Currency,
    bool CanPay,
    IReadOnlyList<PatientClsBatchItem> Items);

public enum ClsPaymentOutcome
{
    /// <summary>Có yêu cầu thanh toán (mới tạo, hoặc yêu cầu đang mở cho đúng các khoản này).</summary>
    Created,

    /// <summary>Không có đợt, hoặc người gọi không có quyền trên hồ sơ bệnh nhân; trả như không tồn tại.</summary>
    NotFound,

    /// <summary>Đợt đã trả đủ, đã hủy hoặc được miễn.</summary>
    NothingToPay,

    /// <summary>Đã hết ngày khám; không nhận thanh toán thêm.</summary>
    PastDeadline,

    /// <summary>Đợt đang có yêu cầu thanh toán mở do người khác tạo (người có quyền khác trên cùng hồ sơ).</summary>
    OpenedByAnotherPayer,
}

public sealed record ClsPaymentRequest(ClsPaymentOutcome Outcome, PaymentDto? Payment);

public sealed record ClsCheckout(ClsPaymentOutcome Outcome, PaymentCheckout? Checkout);

/// <summary>
/// Gom khoản CLS theo đợt chỉ định và tính trạng thái của đợt. Hạn mỗi lần bấm thanh toán là 15 phút, hạn cuối là hết
/// ngày khám D (Phạm vi nghiệp vụ §5.6, Giải pháp kỹ thuật v2.13 mục 5.5).
/// </summary>
public static class ClsBatches
{
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    /// <summary>0 giờ (giờ Việt Nam) của ngày chứa <paramref name="utc"/>, quy về UTC.</summary>
    public static DateTime StartOfDayUtc(DateTime utc) =>
        DateTime.SpecifyKind((utc + IctOffset).Date - IctOffset, DateTimeKind.Utc);

    public static DateTime PayByUtc(DateTime orderedAtUtc) => StartOfDayUtc(orderedAtUtc).AddDays(1);

    /// <param name="items">Các khoản CLS của cùng một đợt chỉ định, không rỗng, đều có EncounterId và PatientId.</param>
    public static PatientClsBatch Build(IReadOnlyCollection<BillingItem> items, DateTime nowUtc)
    {
        var first = items.MinBy(i => i.CreatedAtUtc) ?? throw new ArgumentException("Đợt chỉ định không có khoản thu.", nameof(items));
        var inEffect = items.Where(i => i.IsInEffect).ToList();
        var outstanding = inEffect.Sum(i => i.Outstanding);
        var payBy = PayByUtc(first.CreatedAtUtc);

        var status = inEffect.Count == 0 ? ClsBatchStatus.Cancelled
            : outstanding == 0m ? ClsBatchStatus.Paid
            : nowUtc >= payBy ? ClsBatchStatus.Expired
            : ClsBatchStatus.PendingPayment;

        return new PatientClsBatch(
            first.ServiceOrderId!.Value,
            first.EncounterId!.Value,
            first.PatientId!.Value,
            first.CreatedAtUtc,
            payBy,
            status,
            inEffect.Sum(i => i.TotalAmount),
            inEffect.Sum(i => i.PaidAmount - i.RefundedAmount),
            outstanding,
            BillingCurrency.Vnd,
            status == ClsBatchStatus.PendingPayment,
            items.OrderBy(i => i.CreatedAtUtc).ThenBy(i => i.ItemNameSnapshot, StringComparer.Ordinal).Select(i =>
            {
                var dto = ChargeMapper.ToDto(i);
                return new PatientClsBatchItem(dto.ChargeId, dto.ServiceOrderItemId, dto.ItemNameSnapshot, dto.UnitPrice,
                    dto.Quantity, dto.TotalAmount, i.Outstanding, dto.Status);
            }).ToList());
    }
}

/// <summary>
/// Thanh toán phí CLS trên web bệnh nhân: xem đợt chỉ định trong ngày và trả theo từng đợt qua VNPAY. Chỉ người có quyền
/// đặt lịch trên hồ sơ bệnh nhân (MED-05) mới thấy hoặc trả được; người khác nhận kết quả như không tồn tại.
/// </summary>
public sealed class PatientClsPaymentService(
    IChargeStore charges,
    PaymentService payments,
    PatientPaymentService patientPayments,
    IIdentityPatientsModule identity,
    TimeProvider clock)
{
    /// <summary>Các đợt CLS trong ngày hôm nay (giờ Việt Nam) của bệnh nhân, mới nhất trước.</summary>
    /// <returns>null khi người gọi không có quyền trên hồ sơ.</returns>
    public async Task<IReadOnlyList<PatientClsBatch>?> ListTodayAsync(Guid patientId, Guid userId, CancellationToken ct)
    {
        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct))
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var items = await charges.GetClsByPatientAsync(patientId, ClsBatches.StartOfDayUtc(now), ct);
        return items
            .Where(i => i.ServiceOrderId is not null && i.EncounterId is not null)
            .GroupBy(i => i.ServiceOrderId!.Value)
            .Select(g => ClsBatches.Build(g.ToList(), now))
            .OrderByDescending(b => b.OrderedAtUtc)
            .ToList();
    }

    /// <summary>
    /// Tạo yêu cầu thanh toán cho phần còn phải trả của đợt, hạn 15 phút nhưng không quá hết ngày khám.
    /// Gọi lại khi yêu cầu cũ còn mở cho đúng các khoản đó thì nhận lại yêu cầu cũ.
    /// </summary>
    /// <exception cref="BillingException">PaymentConflict khi đang có yêu cầu mở cho một phần khác của đợt; ChargeNotPayable khi
    /// khoản vừa được trả hoặc hủy giữa chừng.</exception>
    public async Task<ClsPaymentRequest> CreatePaymentAsync(Guid serviceOrderId, Guid userId, CancellationToken ct)
    {
        var items = await charges.GetClsByServiceOrderAsync(serviceOrderId, ct);
        if (items.Count == 0 || items.Any(i => i.PatientId is null || i.EncounterId is null)
            || items.Select(i => i.PatientId).Distinct().Count() != 1)
            return new ClsPaymentRequest(ClsPaymentOutcome.NotFound, null);

        var patientId = items.First().PatientId!.Value;
        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct))
            return new ClsPaymentRequest(ClsPaymentOutcome.NotFound, null);

        var now = clock.GetUtcNow().UtcDateTime;
        var batch = ClsBatches.Build(items, now);
        switch (batch.Status)
        {
            case ClsBatchStatus.Paid or ClsBatchStatus.Cancelled:
                return new ClsPaymentRequest(ClsPaymentOutcome.NothingToPay, null);
            case ClsBatchStatus.Expired:
                return new ClsPaymentRequest(ClsPaymentOutcome.PastDeadline, null);
        }

        var expiresAt = now + ClsBatches.PaymentWindow < batch.PayByUtc ? now + ClsBatches.PaymentWindow : batch.PayByUtc;
        var payment = await payments.CreateAsync(new CreatePayment(
            PaymentPurpose.CLSOrderBatch,
            patientId,
            userId,
            BookingAttemptId: null,
            batch.EncounterId,
            items.Where(i => i.Outstanding > 0m).Select(i => i.Id).ToList(),
            PaymentProvider.VNPAY,
            expiresAt), ct);

        return new ClsPaymentRequest(ClsPaymentOutcome.Created, payment);
    }

    /// <summary>Tạo (hoặc lấy lại) yêu cầu thanh toán của đợt rồi tạo liên kết VNPAY. Lời gọi cổng nằm ngoài transaction DB (MOD-05).</summary>
    /// <exception cref="BillingException">Như <see cref="CreatePaymentAsync"/> và <see cref="PaymentCheckoutService.StartAsync"/>.</exception>
    public async Task<ClsCheckout> StartCheckoutAsync(Guid serviceOrderId, Guid userId, string? customerIp, CancellationToken ct)
    {
        var request = await CreatePaymentAsync(serviceOrderId, userId, ct);
        if (request.Payment is null)
            return new ClsCheckout(request.Outcome, null);

        // Yêu cầu đang mở có thể do người khác cùng quyền trên hồ sơ tạo: chỉ người trả của yêu cầu mới lấy được liên kết.
        var checkout = await patientPayments.StartCheckoutAsync(request.Payment.PaymentIntentId, userId, customerIp, ct);
        return checkout is null
            ? new ClsCheckout(ClsPaymentOutcome.OpenedByAnotherPayer, null)
            : new ClsCheckout(ClsPaymentOutcome.Created, checkout);
    }
}
