namespace Shared.Infrastructure.Persistence.Entities;

public sealed class ApiIdempotencyRecord
{
    public Guid Id { get; set; }
    // ID chéo module: workflow.Operation, không FK
    public Guid? OperationId { get; set; }
    public string CallerScope { get; set; } = null!;
    public string OperationType { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBodyJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
