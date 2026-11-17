namespace Billing.Contracts;

/// <summary>
/// Contract trong tiến trình cho Clinical: gắn phí khám ban đầu đã thu lúc đặt lịch vào lượt khám khi lượt khám hình thành
/// (Phạm vi nghiệp vụ §5.5). Tách khỏi <see cref="IBillingModule"/> để module chỉ gọi báo giá/tạo khoản không phải cài thêm hàm.
/// </summary>
public interface IEncounterChargeLinker
{
    /// <summary>
    /// Clinical gọi trong unit of work check-in, sau khi tạo Encounter. Gọi lại được. Lịch hẹn chưa có phí khám gắn với nó
    /// (chưa thu, hoặc Billing chưa nhận AppointmentConfirmed) thì không làm gì và trả 0.
    /// </summary>
    /// <returns>Số khoản phí khám đang gắn với lượt khám này.</returns>
    /// <exception cref="BillingException">InvalidRequest: thiếu Id, hoặc phí khám đã gắn lượt khám/bệnh nhân khác.</exception>
    Task<int> LinkEncounterAsync(LinkEncounter command, CancellationToken ct);
}
