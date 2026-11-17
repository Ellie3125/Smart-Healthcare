using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

// Chỉ ghi thêm: mỗi lần trả, sửa hoặc gỡ kết quả CLS ghi một dòng chứa nội dung SAU thao tác đó (Giải pháp §6.5 "Sửa nhầm").
// Tệp kết quả do DocumentsReporting giữ; bảng này chỉ giữ phần chữ của Clinical.
public sealed class CLSResultRevision
{
    public const int ReasonMaxLength = 1000;

    public Guid Id { get; set; }
    public Guid CLSResultId { get; set; }
    public int RevisionNumber { get; set; }
    public ClsResultRevisionKind Kind { get; set; }
    public string ResultSummary { get; set; } = null!;
    public string DetailedFindings { get; set; } = null!;
    // Bắt buộc khi Kind = Corrected hoặc Withdrawn.
    public string? Reason { get; set; }
    // ID chéo module: identity.AppUser (tài khoản phòng đang thao tác), không FK
    public Guid ChangedByAccountId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
