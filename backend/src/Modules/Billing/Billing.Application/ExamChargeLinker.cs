using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Billing.Application;

/// <summary>
/// Gắn phí khám ban đầu (thu lúc đặt lịch, khi chưa có lịch hẹn hay lượt khám) với lịch hẹn rồi với lượt khám, để tổng hợp
/// chi phí lượt khám có cả phí khám (Phạm vi nghiệp vụ §5.5). Hai bước:
/// 1. <see cref="AttachAppointmentAsync"/> khi Scheduling xác nhận lịch hoặc đổi lịch (AppointmentConfirmed).
/// 2. <see cref="LinkEncounterAsync"/> khi Clinical mở lượt khám lúc check-in.
/// Mỗi bước chỉ ghi giá trị xác định từ sự kiện nên chạy lại hoặc nhận lặp không đổi kết quả.
/// </summary>
public sealed class ExamChargeLinker(IChargeStore charges, IPaymentStore store) : IEncounterChargeLinker
{
    /// <summary>
    /// Gắn AppointmentId, PatientId vào phí khám đã thu của lượt đặt lịch. Đổi lịch: chuyển từ lịch cũ sang lịch mới. Sự kiện đến
    /// sai thứ tự không ghi đè: xác nhận lần đầu chỉ gắn khi khoản chưa có lịch; đổi lịch chỉ chuyển khi khoản đang ở lịch cũ.
    /// Khoản đã gắn lượt khám thì giữ nguyên.
    /// </summary>
    /// <returns>Số khoản vừa đổi.</returns>
    public async Task<int> AttachAppointmentAsync(AppointmentConfirmed e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var ids = await charges.FindExamChargeIdsAsync(e.BookingAttemptId, e.RescheduledFromAppointmentId, ct);
        if (ids.Count == 0)
            return 0;

        return await store.InTransactionAsync(async token =>
        {
            var changed = 0;
            foreach (var charge in await store.LockChargesAsync(ids, token))
            {
                var applies = charge.EncounterId is null && charge.AppointmentId != e.AppointmentId
                    && (charge.AppointmentId is null
                        || (e.RescheduledFromAppointmentId is { } from && charge.AppointmentId == from));
                if (!applies)
                    continue;

                charge.AppointmentId = e.AppointmentId;
                charge.PatientId ??= e.PatientId;
                changed++;
            }

            if (changed > 0)
                await store.SaveChangesAsync(token);
            return changed;
        }, ct);
    }

    public async Task<int> LinkEncounterAsync(LinkEncounter command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AppointmentId == Guid.Empty || command.EncounterId == Guid.Empty || command.PatientId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu AppointmentId, EncounterId hoặc PatientId.");

        var ids = await charges.FindExamChargeIdsByAppointmentAsync(command.AppointmentId, ct);
        if (ids.Count == 0)
            return 0;

        return await store.InTransactionAsync(async token =>
        {
            var locked = (await store.LockChargesAsync(ids, token))
                .Where(c => c.AppointmentId == command.AppointmentId && c.ItemType == ItemType.ConsultationFee)
                .ToList();
            foreach (var charge in locked)
            {
                if ((charge.EncounterId is { } encounter && encounter != command.EncounterId)
                    || (charge.PatientId is { } patient && patient != command.PatientId))
                    throw new BillingException(BillingErrorCode.InvalidRequest,
                        $"Phí khám {charge.Id} của lịch hẹn {command.AppointmentId} đã gắn lượt khám hoặc bệnh nhân khác.");
            }

            var toLink = locked.Where(c => c.EncounterId is null).ToList();
            foreach (var charge in toLink)
            {
                charge.EncounterId = command.EncounterId;
                charge.PatientId = command.PatientId;
            }

            if (toLink.Count > 0)
                await store.SaveChangesAsync(token);
            return locked.Count;
        }, ct);
    }
}

/// <summary>Scheduling xác nhận lịch (sau PaymentVerified) hoặc đổi lịch → gắn phí khám đã thu với lịch hẹn.</summary>
public sealed class AppointmentConfirmedHandler(ExamChargeLinker linker) : IIntegrationEventHandler<AppointmentConfirmed>
{
    public Task HandleAsync(AppointmentConfirmed e, CancellationToken ct) => linker.AttachAppointmentAsync(e, ct);
}
