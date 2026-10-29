using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Workflow.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Workflow.Infrastructure.Configurations;

internal sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Operation), table =>
        {
            table.HasEnumCheck<OperationType>(nameof(Operation.OperationType));
            table.HasEnumCheck<TargetEntityType>(nameof(Operation.TargetEntityType));
            table.HasEnumCheck<OperationStatus>(nameof(Operation.Status));
        });
        builder.Property(e => e.OperationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.TargetEntityType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.FailureReason).HasMaxLength(2000);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.InitiatedByUserId); // tham chiếu identity.AppUser, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.BookingAttempt).WithMany().HasForeignKey(e => e.BookingAttemptId).OnDelete(DeleteBehavior.Restrict);
    }
}
