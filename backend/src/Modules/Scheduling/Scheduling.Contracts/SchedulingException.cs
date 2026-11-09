namespace Scheduling.Contracts;

/// <summary>Mã lỗi Scheduling trả cho module gọi; cùng tên với extension <c>code</c> của ProblemDetails (docs/conventions.md §4).</summary>
public enum SchedulingErrorCode
{
    /// <summary>Tham số không hợp lệ (danh sách rỗng hoặc trùng, Guid rỗng, quá giới hạn).</summary>
    InvalidInput,

    /// <summary>Không có lịch hẹn với Id này, hoặc người gọi không được biết nó tồn tại.</summary>
    AppointmentNotFound,

    /// <summary>Trạng thái lịch hẹn không cho phép thao tác (ví dụ check in lịch Cancelled, Absent, Rescheduled, Completed).</summary>
    InvalidAppointmentState,

    /// <summary>Chưa tới ngày phục vụ D theo giờ Việt Nam.</summary>
    CheckInNotOpen,

    /// <summary>Giờ DB đã qua CheckInDeadlineUtc của phiên; vé chuyển Vắng mặt, không nhận (Phạm vi §4.2).</summary>
    CheckInDeadlinePassed,

    /// <summary>EncounterId không có vé khám đầu đã check in.</summary>
    InitialTicketNotFound,

    /// <summary>Dịch vụ CLS không tồn tại, ngừng hoạt động hoặc không còn phòng/vị trí hoạt động (Phạm vi §6.3).</summary>
    MedicalServiceUnavailable,
}

/// <summary>Lỗi nghiệp vụ Scheduling trả cho module gọi. Không retry: dữ liệu hoặc trạng thái phải đổi trước.</summary>
public sealed class SchedulingException(SchedulingErrorCode code, string message) : Exception(message)
{
    public SchedulingErrorCode Code { get; } = code;
}
