using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class DoctorSessionSlotQuotaConfiguration : IEntityTypeConfiguration<DoctorSessionSlotQuota>
{
    public void Configure(EntityTypeBuilder<DoctorSessionSlotQuota> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(DoctorSessionSlotQuota), table =>
        {
            table.HasEnumCheck<SessionStatus>(nameof(DoctorSessionSlotQuota.SessionStatus));
            table.HasCheckConstraint("CK_DoctorSessionSlotQuota_HeldCount", "[HeldCount] >= 0");
            table.HasCheckConstraint("CK_DoctorSessionSlotQuota_AdmittedCount", "[AdmittedCount] >= 0");
            table.HasCheckConstraint("CK_DoctorSessionSlotQuota_LockedCancelledCount", "[LockedCancelledCount] >= 0");
            table.HasCheckConstraint("CK_DoctorSessionSlotQuota_Capacity", "[HeldCount] + [AdmittedCount] + [LockedCancelledCount] <= [Capacity]");
        });
        builder.Property(e => e.SessionStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        // DoctorId: tham chiếu identity.Doctor, không FK chéo schema (MOD-01); index ghép phía dưới dẫn đầu bằng cột này
        builder.HasIndex(e => e.AssignedRoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
        builder.HasIndex(e => new { e.DoctorId, e.ServiceDate }).IsUnique();
    }
}
