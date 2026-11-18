using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence.Configurations;

internal sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("Operation");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TargetEntityType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.InitiatedByUserId);
        builder.HasIndex(x => x.BookingAttemptId);
        // Giám sát Operation lỗi/treo (W4-04) đọc theo trạng thái + thời điểm tạo; Succeeded (gần như toàn bảng) không vào index.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc }, "IX_Operation_Unfinished")
            .IncludeProperties(x => new { x.OperationType, x.TargetEntityId })
            .HasFilter("[Status] IN (N'Pending', N'Running', N'WaitingExternal', N'Failed', N'NeedsAction', N'ManualReview')");
    }
}
