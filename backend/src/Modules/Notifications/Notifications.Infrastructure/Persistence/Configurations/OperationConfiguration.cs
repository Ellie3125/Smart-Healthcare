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
    }
}
