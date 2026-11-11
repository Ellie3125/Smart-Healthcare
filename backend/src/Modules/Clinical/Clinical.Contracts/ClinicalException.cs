namespace Clinical.Contracts;

public enum ClinicalErrorCode
{
    /// <summary>Yêu cầu không hợp lệ (tham số rỗng, sai định dạng, ngày giờ không phải UTC...).</summary>
    InvalidRequest,

    /// <summary>Không tìm thấy lượt khám tương ứng với EncounterId.</summary>
    EncounterNotFound,

    /// <summary>Bệnh nhân đã có lượt khám đang mở (InProgress, WaitingCLS, ReturningDoctor) trong cùng phòng khám hoặc ca khám.</summary>
    ActiveEncounterAlreadyExists,

    /// <summary>Chuyển đổi trạng thái lượt khám không hợp lệ theo quy trình nghiệp vụ.</summary>
    InvalidStatusTransition,

    /// <summary>Chỉ số sinh tồn nằm ngoài dải giá trị sinh lý cho phép.</summary>
    VitalSignsOutOfRange,

    /// <summary>Lý do đến khám (Chief Complaint) bắt buộc phải có trước khi khám hoặc chỉ định.</summary>
    ChiefComplaintRequired,

    /// <summary>Đợt chỉ định cận lâm sàng không tìm thấy.</summary>
    OrderNotFound,

    /// <summary>Dòng chỉ định cận lâm sàng không tìm thấy.</summary>
    OrderItemNotFound,

    /// <summary>Đợt chỉ định đã được xác nhận (Confirmed), không thể chỉnh sửa thêm bớt mục.</summary>
    OrderAlreadyConfirmed,

    /// <summary>Dòng dịch vụ CLS bị trùng trong cùng một phiếu chỉ định.</summary>
    DuplicateOrderItem,

    /// <summary>Không thể hủy dịch vụ CLS khi đã lấy mẫu hoặc đang/đã thực hiện.</summary>
    CannotCancelOrderItem,

    /// <summary>Không tìm thấy kết quả cận lâm sàng.</summary>
    ResultNotFound,

    /// <summary>Kết quả cận lâm sàng đã được xuất bản (Published), không thể chỉnh sửa hoặc hủy trực tiếp.</summary>
    ResultAlreadyPublished,

    /// <summary>Không tìm thấy đơn thuốc tương ứng.</summary>
    PrescriptionNotFound,

    /// <summary>Đơn thuốc đã được ký phát hành (Issued), không thể chỉnh sửa dòng thuốc.</summary>
    PrescriptionAlreadyIssued,

    /// <summary>Đơn thuốc không thể phát hành khi chưa có mục thuốc nào.</summary>
    EmptyPrescription,

    /// <summary>Không thể hoàn thành lượt khám khi chưa có ít nhất một chẩn đoán xác định (Final Diagnosis) là chẩn đoán chính (Primary).</summary>
    CannotCompleteWithoutFinalDiagnosis,

    /// <summary>Vẫn còn dịch vụ cận lâm sàng bắt buộc chưa hoàn thành hoặc chưa có kết quả công bố.</summary>
    PendingMandatoryClsOrders,

    /// <summary>Không thể hoàn thành lượt khám khi các điều kiện tiên quyết y tế chưa thỏa mãn.</summary>
    CannotCompleteEncounter,

    /// <summary>Xung đột dữ liệu đồng thời (RowVersion đã bị thay đổi bởi phiên làm việc khác).</summary>
    ConcurrencyConflict,
}

/// <summary>Lỗi nghiệp vụ Clinical trả cho client hoặc các module gọi khác.</summary>
public sealed class ClinicalException(ClinicalErrorCode code, string message) : Exception(message)
{
    public ClinicalErrorCode Code { get; } = code;
}

