using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessage");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.AggregateType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        // ProcessedAtUtc = lần xử lý gần nhất (nhận, lỗi hoặc Published): dispatcher tính backoff/lease, job dọn tính tuổi bản ghi.
        // Trưởng nhóm chốt 17/11/2026 giữ một cột, không tách NextAttemptAtUtc.
        builder.HasIndex(x => x.OperationId);
        // Chỉ bản ghi chưa xong (Published bị loại khỏi index nên index nhỏ dù bảng lớn): Worker claim và giám sát ngoại lệ
        // đọc theo trạng thái + thời điểm tạo mà không quét bảng (CLA-PERF-001).
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc }, "IX_OutboxMessage_Unfinished")
            .IncludeProperties(x => new { x.EventType, x.RetryCount, x.ProcessedAtUtc, x.AggregateId, x.OperationId })
            .HasFilter("[Status] IN (N'Pending', N'Processing', N'DeadLetter')");
    }
}
