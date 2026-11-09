namespace Shared.Abstractions;

/// <summary>
/// Idempotency của lệnh thay đổi (Giải pháp v2.13 §4, DB-04): bản ghi theo bộ ba (callerScope, operationType, key)
/// lưu RequestHash, OperationId và kết quả trong DB. Hai Backend nhận cùng khóa tranh nhau tại DB bằng khóa hàng.
/// Phải gọi bên trong <see cref="IUnitOfWork"/>; gọi ngoài transaction là lỗi.
/// </summary>
/// <remarks>
/// Lệnh đồng bộ: <see cref="BeginAsync"/>, làm tác dụng nghiệp vụ, rồi <see cref="CompleteAsync"/> trong CÙNG transaction.
/// Bản ghi chưa có kết quả là đang chạy, không bao giờ coi là thành công.
/// </remarks>
public interface IIdempotencyStore
{
    /// <param name="requestHash">Hash của nội dung yêu cầu đã chuẩn hóa; cùng khóa khác hash là <see cref="IdempotencyBeginResult.Conflict"/>.</param>
    /// <param name="ttl">Thời gian giữ bản ghi, tính theo giờ DB; bản ghi quá hạn bị ghi đè như khóa mới.</param>
    /// <param name="operationId">workflow.Operation của lệnh, nếu có; trả lại trong <see cref="IdempotencyBeginResult.InProgress"/>.</param>
    Task<IdempotencyBeginResult> BeginAsync(string callerScope, string operationType, string key, string requestHash,
        TimeSpan ttl, Guid? operationId, CancellationToken ct);

    /// <summary>Ghi kết quả cùng transaction với tác dụng nghiệp vụ. Bản ghi phải đã được tạo bằng <see cref="BeginAsync"/>.</summary>
    Task CompleteAsync(string callerScope, string operationType, string key, int statusCode, string? bodyJson,
        Guid? operationId, CancellationToken ct);
}

/// <summary>Kết quả của <see cref="IIdempotencyStore.BeginAsync"/>.</summary>
public abstract record IdempotencyBeginResult
{
    private IdempotencyBeginResult() { }

    /// <summary>Khóa mới (hoặc bản ghi cũ đã hết hạn): bên gọi thực hiện lệnh rồi gọi CompleteAsync.</summary>
    public sealed record Started : IdempotencyBeginResult;

    /// <summary>Đã có kết quả với cùng nội dung: trả lại đúng kết quả cũ, không làm lại.</summary>
    public sealed record Replay(int StatusCode, string? BodyJson, Guid? OperationId) : IdempotencyBeginResult;

    /// <summary>Cùng nội dung nhưng chưa có kết quả: trả trạng thái đang xử lý, không làm lại.</summary>
    public sealed record InProgress(Guid? OperationId) : IdempotencyBeginResult;

    /// <summary>Cùng khóa nhưng khác nội dung: từ chối.</summary>
    public sealed record Conflict : IdempotencyBeginResult;
}
