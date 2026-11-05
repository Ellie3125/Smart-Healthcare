namespace Shared.Abstractions;

/// <summary>
/// Chạy một thao tác nguyên tử: mở một DbTransaction trên DbConnection dùng chung, cho DbContext của các module
/// tham gia (ví dụ Clinical, Scheduling, Billing khi chỉ định CLS), commit một lần (Giải pháp v2.11 §8.2).
/// Khi SQL Server báo deadlock 1205, transaction đã bị rollback; bản cài đặt chạy lại TOÀN BỘ <c>work</c>
/// từ dữ liệu mới, tối đa 3 lần, có backoff và jitter (Giải pháp v2.11 §3.1).
/// </summary>
/// <remarks>
/// Vì <c>work</c> có thể chạy lại tối đa 3 lần:
/// <list type="bullet">
/// <item>KHÔNG gọi dịch vụ ngoài bên trong (cổng thanh toán, R2, model, gửi thông báo); việc đó đi qua Outbox sau commit.</item>
/// <item>Đọc lại trạng thái và kiểm lại điều kiện trong <c>work</c>, không dùng giá trị đọc trước khi gọi.</item>
/// <item>Không giữ tác dụng phụ trong bộ nhớ (biến đếm, danh sách) qua các lần chạy.</item>
/// </list>
/// Gọi lồng khi đã có transaction đang mở thì tham gia transaction đó, không commit thay bên ngoài.
/// </remarks>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
