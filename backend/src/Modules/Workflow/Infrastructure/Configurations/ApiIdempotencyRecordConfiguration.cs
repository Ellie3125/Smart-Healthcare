using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Workflow.Domain;

namespace SmartHealthcare.Workflow.Infrastructure.Configurations;

internal sealed class ApiIdempotencyRecordConfiguration : IEntityTypeConfiguration<ApiIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<ApiIdempotencyRecord> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CallerScope).HasMaxLength(128);
        builder.Property(e => e.OperationType).HasMaxLength(128);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(128);
        builder.Property(e => e.RequestHash).HasMaxLength(64);
        builder.Property(e => e.ResponseBodyJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(e => new { e.CallerScope, e.OperationType, e.IdempotencyKey }).IsUnique().HasDatabaseName("UK_Idem");
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
