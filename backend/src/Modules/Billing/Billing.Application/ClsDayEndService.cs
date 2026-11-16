using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>
/// Job cuối ngày khám D (Giải pháp kỹ thuật v2.13 mục 5.5, Phạm vi nghiệp vụ §5.6): khoản phí CLS chưa trả của những ngày đã
/// qua chuyển Đã hủy, để lượt khám không treo nghĩa vụ thanh toán. Vé CLS do Scheduling hủy trong job của mình; dịch vụ đã
/// trả mà bệnh nhân bỏ không làm không hoàn tiền, nên Billing không đổi khoản đã trả.
/// </summary>
public sealed class ClsDayEndService(IChargeStore charges, IPaymentStore store, TimeProvider clock)
{
    public const int BatchSize = 200;

    /// <summary>
    /// Hủy các khoản CLS còn Pending tạo trước 0 giờ hôm nay (giờ Việt Nam), mỗi lô một transaction. Chạy lại được: khoản đã
    /// hủy không bị chọn lại. Khoản PartiallyPaid giữ nguyên để nhân viên đối soát (đã thu một phần thật).
    /// IPN đến muộn cho khoản đã hủy vẫn được ghi nhận và tính vào tiền chờ hoàn (BillingItem.ApplyPayment).
    /// </summary>
    /// <returns>Số khoản đã hủy.</returns>
    public async Task<int> CancelUnpaidAsync(CancellationToken ct)
    {
        var cutoff = ClsBatches.StartOfDayUtc(clock.GetUtcNow().UtcDateTime);
        var cancelled = 0;
        while (true)
        {
            var due = await charges.FindUnpaidClsBeforeAsync(cutoff, BatchSize, ct);
            if (due.Count == 0)
                return cancelled;

            var done = await store.InTransactionAsync(async token =>
            {
                // Khóa rồi kiểm lại: IPN có thể vừa ghi nhận tiền cho khoản này (cùng thứ tự khóa theo Id, DB-09).
                var locked = await store.LockChargesAsync(due, token);
                var toCancel = locked.Where(c => c.Status == BillingItemStatus.Pending && c.CreatedAtUtc < cutoff).ToList();
                foreach (var charge in toCancel)
                    charge.Status = BillingItemStatus.Cancelled;

                await store.SaveChangesAsync(token);
                return toCancel.Count;
            }, ct);

            cancelled += done;

            // Không hủy được khoản nào trong lô (bên khác vừa đổi trạng thái hết): dừng, lần chạy sau xét lại, không lặp vô hạn.
            if (done == 0)
                return cancelled;
        }
    }
}
