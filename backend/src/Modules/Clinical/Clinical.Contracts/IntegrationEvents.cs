using Shared.Abstractions;

namespace Clinical.Contracts;

/// <summary>
/// Phát ra khi một lượt khám bệnh bắt đầu được tiếp nhận tại phòng khám (Encounter.InProgress).
/// Module Scheduling lắng nghe để chuyển Appointment sang trạng thái tương ứng.
/// </summary>
public sealed record EncounterStartedIntegrationEvent(
    Guid EventId,
    Guid EncounterId,
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}

/// <summary>
/// Phát ra khi bác sĩ xác nhận một đợt chỉ định cận lâm sàng (ServiceOrder.Confirmed).
/// </summary>
public sealed record ClsOrderConfirmedIntegrationEvent(
    Guid EventId,
    Guid EncounterId,
    Guid ServiceOrderId,
    int OrderRoundNumber,
    IReadOnlyList<Guid> ServiceOrderItemIds,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}

/// <summary>
/// Phát ra khi toàn bộ các kết quả cận lâm sàng của đợt khám đã hoàn tất và được duyệt công bố (Published),
/// hệ thống cấp vé quay lại phòng khám ban đầu cho bệnh nhân.
/// </summary>
public sealed record ClsResultsPublishedIntegrationEvent(
    Guid EventId,
    Guid EncounterId,
    Guid ServiceOrderId,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}

/// <summary>
/// Phát ra khi lượt khám hoàn tất thành công (Encounter.Completed).
/// Module DocumentsReporting lắng nghe để sinh phiếu tổng kết bệnh án / đơn thuốc PDF;
/// Module Billing đối soát chi phí; Module Notifications báo cho bệnh nhân.
/// </summary>
public sealed record EncounterCompletedIntegrationEvent(
    Guid EventId,
    Guid EncounterId,
    Guid AppointmentId,
    Guid PatientId,
    DateTime OccurredAtUtc,
    Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}

