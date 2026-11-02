using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Persistence.Entities;

namespace Shared.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TableName).HasMaxLength(200);
        builder.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.DeviceId).HasMaxLength(64);
        builder.Property(x => x.ClientIpAddress).HasMaxLength(64);
        builder.Property(x => x.ChangedColumns).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ChangeReason).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.PerformedByUserId);
        builder.HasIndex(x => x.RoomAccountId);
        builder.HasIndex(x => x.RoomDeviceSessionId);
        builder.HasIndex(x => x.OperationId);
        builder.HasIndex(x => x.StaffShiftAssignmentId);
    }
}
