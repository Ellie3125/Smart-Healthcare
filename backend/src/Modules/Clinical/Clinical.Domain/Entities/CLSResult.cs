using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

public sealed class CLSResult
{
    public Guid Id { get; set; }
    public Guid ServiceOrderItemId { get; set; }
    public string ResultSummary { get; set; } = null!;
    public string DetailedFindings { get; set; } = null!;
    public ResultStatus ResultStatus { get; set; }
    // ID chéo module: identity.Staff, không FK
    public Guid? PerformedByStaffId { get; set; }
    // ID chéo module: identity.Staff, không FK
    public Guid? ApprovedByStaffId { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
