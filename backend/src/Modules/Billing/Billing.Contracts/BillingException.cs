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

    /// <summary>Khoản thu không tồn tại, đã hủy/miễn/trả đủ, hoặc không đúng loại/lượt khám của yêu cầu thanh toán.</summary>
    ChargeNotPayable,

    /// <summary>Đã có yêu cầu thanh toán đang mở chồng lên yêu cầu này (khác khoản hoặc khác cổng); hoặc giao dịch đã ghi nhận bằng mã khác.</summary>
    PaymentConflict,

    /// <summary>Không có giao dịch với mã tham chiếu này.</summary>
    PaymentNotFound,

    /// <summary>Số tiền hoặc loại tiền cổng báo không khớp giao dịch; không ghi nhận đã trả (BR-002).</summary>
    AmountMismatch,

    /// <summary>Không gọi được cổng thanh toán hoặc cổng chưa được cấu hình; yêu cầu thanh toán vẫn giữ, gọi lại để lấy liên kết.</summary>
    GatewayUnavailable,

    /// <summary>Kết quả gửi về không có chữ ký hợp lệ của cổng (sai khóa, sửa nội dung, quá hạn); không ghi nhận gì.</summary>
    InvalidSignature,
}

/// <summary>Lỗi nghiệp vụ Billing trả cho module gọi. Không retry: dữ liệu hoặc cấu hình phải được sửa trước (DB-12).</summary>
public sealed class BillingException(BillingErrorCode code, string message) : Exception(message)
{
    public BillingErrorCode Code { get; } = code;
}
