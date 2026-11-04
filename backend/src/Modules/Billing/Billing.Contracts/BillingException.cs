namespace Billing.Contracts;

public enum BillingErrorCode
{
    /// <summary>Tham số không hợp lệ (thời điểm không phải UTC, danh sách rỗng, số lượng không dương...).</summary>
    InvalidRequest,

    /// <summary>Không có đơn giá đang hiệu lực cho bác sĩ/cấp bậc hoặc dịch vụ tại thời điểm áp giá.</summary>
    PriceNotConfigured,

    /// <summary>Có nhiều hơn một đơn giá cùng căn cứ đang hiệu lực; lỗi cấu hình bảng giá, không tự chọn.</summary>
    AmbiguousPrice,

    /// <summary>Một ServiceOrderItem đang có nhiều hơn một khoản thu chưa hủy; dữ liệu cần đối soát, không tự chọn.</summary>
    DuplicateCharge,
}

/// <summary>Lỗi nghiệp vụ Billing trả cho module gọi. Không retry: dữ liệu hoặc cấu hình phải được sửa trước (DB-12).</summary>
public sealed class BillingException(BillingErrorCode code, string message) : Exception(message)
{
    public BillingErrorCode Code { get; } = code;
}
