using Notifications.Domain.Entities;
using Notifications.Domain.Enums;

namespace Notifications.Application.PatientNotifications;

public sealed record PatientNotificationItem(Guid Id, string Title, string Body, string Category, string Status, DateTime CreatedAt, string? TargetPath);
public sealed record PatientNotificationPage(IReadOnlyList<PatientNotificationItem> Items, int UnreadCount, bool HasMore);
public enum PatientNotificationError { InvalidInput, NotFound, IdempotencyConflict }
public sealed class PatientNotificationException(PatientNotificationError error) : Exception(error.ToString()) { public PatientNotificationError Error { get; } = error; }
public interface IPatientNotificationReader
{
    Task<PatientNotificationPage> ListAsync(Guid userId, Guid patientId, bool unreadOnly, int page, int pageSize, CancellationToken ct);
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct);
}

public static class PatientNotificationMessages
{
    public static PatientNotificationItem Create(PatientNotification notification, bool isRead)
    {
        var (title, body, category) = notification.Kind switch
        {
            PatientNotificationKind.AppointmentConfirmed => ("Lịch hẹn đã được xác nhận", "Bạn có thể xem vé và thông tin lịch hẹn.", "Appointment"),
            PatientNotificationKind.AppointmentCancelled => ("Lịch hẹn đã được hủy", "Vui lòng xem trạng thái lịch hẹn và thanh toán.", "Appointment"),
            PatientNotificationKind.DoctorChanged => ("Phiên khám đã đổi bác sĩ", "Vui lòng xem lịch hẹn hoặc liên hệ quầy tiếp đón.", "Appointment"),
            PatientNotificationKind.ClsOrdered => ("Có chỉ định cận lâm sàng", "Vui lòng xem chỉ định và các khoản phí trong lượt khám.", "Clinical"),
            PatientNotificationKind.ResultsAvailable => ("Kết quả đã được phát hành", "Bạn có thể xem kết quả đã được bác sĩ kết luận.", "Clinical"),
            PatientNotificationKind.TicketCalled => ("Đã đến lượt của bạn", "Vui lòng đến phòng phục vụ để được hướng dẫn.", "Queue"),
            PatientNotificationKind.ReturnTicketActivated => ("Vé quay lại đã được kích hoạt", "Vui lòng quay lại phòng khám theo hướng dẫn.", "Queue"),
            _ => throw new InvalidOperationException("Unknown patient notification kind.")
        };
        return new(notification.Id, title, body, category, isRead ? "Read" : "Sent", notification.CreatedAtUtc, null);
    }
}
