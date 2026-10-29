using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Workflow.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Workflow.Infrastructure.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(AuditLog), table =>
        {
            table.HasEnumCheck<ActionType>(nameof(AuditLog.ActionType));
        });
        builder.Property(e => e.TableName).HasMaxLength(128);
        builder.Property(e => e.ActionType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.DeviceId).HasMaxLength(128);
        builder.Property(e => e.ClientIpAddress).HasMaxLength(45);
        builder.Property(e => e.ChangedColumns).HasMaxLength(2000);
        builder.Property(e => e.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.NewValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.ChangeReason).HasMaxLength(500);
        builder.HasIndex(e => e.PerformedByUserId); // tham chiếu identity.AppUser, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.RoomAccountId); // tham chiếu identity.RoomAccount, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.RoomDeviceSessionId); // tham chiếu identity.RoomDeviceSession, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.StaffShiftAssignmentId); // tham chiếu identity.StaffShiftAssignment, không FK chéo schema (MOD-01)
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
