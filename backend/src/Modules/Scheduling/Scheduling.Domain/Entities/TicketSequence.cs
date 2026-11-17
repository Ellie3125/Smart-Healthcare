namespace Scheduling.Domain.Entities;

/// <summary>
/// Bộ đếm số vé theo phạm vi (CLA-PERF-001, 17/11/2026), thay cho MAX(SequenceNumber) theo tiền tố TicketNumber.
/// Scope là tiền tố số vé: khám đầu <c>{quotaId:N}-</c>, quay lại <c>{quotaId:N}-R</c>, CLS <c>{roomId:N}-{yyyyMMdd}-</c>.
/// Một bên ghi: store cấp số (check-in, vé CLS/quay lại), tăng trong cùng transaction tạo vé.
/// </summary>
public sealed class TicketSequence
{
    public string Scope { get; set; } = null!;
    public int LastValue { get; set; }
}
