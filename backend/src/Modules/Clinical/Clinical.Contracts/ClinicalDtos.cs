namespace Clinical.Contracts;

/// <summary>
/// DTO chứa thông tin kết quả CLS phục vụ liên module (GP §6.5).
/// </summary>
public sealed record ClsResultInfoDto(
    Guid Id,
    Guid ServiceOrderItemId,
    string ResultStatus,
    Guid? ApprovedByStaffId,
    DateTime? PublishedAtUtc);
