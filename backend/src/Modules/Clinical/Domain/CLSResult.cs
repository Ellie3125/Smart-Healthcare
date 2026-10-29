namespace SmartHealthcare.Clinical.Domain;

public sealed class CLSResult
{
    public Guid Id { get; set; }
    public Guid ServiceOrderItemId { get; set; }
    public ServiceOrderItem ServiceOrderItem { get; set; } = null!;
    public string? ResultSummary { get; set; }
    public string? DetailedFindings { get; set; }
    public ResultStatus ResultStatus { get; set; }
    public Guid PerformedByStaffId { get; set; }
    public Guid? ApprovedByStaffId { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
