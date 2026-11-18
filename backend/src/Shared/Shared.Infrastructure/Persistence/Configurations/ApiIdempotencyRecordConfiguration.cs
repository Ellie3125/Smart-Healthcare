using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure.Persistence.Configurations;

internal sealed class ApiIdempotencyRecordConfiguration : IEntityTypeConfiguration<ApiIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<ApiIdempotencyRecord> builder)
    {
        builder.ToTable("ApiIdempotencyRecord");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CallerScope).HasMaxLength(64);
        builder.Property(x => x.OperationType).HasMaxLength(64);
        // Các module nhận key 1–128 ký tự theo contract; Scheduling tự giới hạn 64.
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.RequestHash).HasMaxLength(128);
        builder.Property(x => x.ResponseBodyJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.CallerScope, x.OperationType, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.OperationId);
    }
}
