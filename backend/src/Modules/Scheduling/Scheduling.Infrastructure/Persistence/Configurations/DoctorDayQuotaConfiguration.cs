using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class DoctorDayQuotaConfiguration : IEntityTypeConfiguration<DoctorDayQuota>
{
    public void Configure(EntityTypeBuilder<DoctorDayQuota> builder)
    {
        builder.ToTable("DoctorDayQuota", t =>
        {
            t.HasCheckConstraint("CK_DoctorDayQuota_Counts_NonNegative", "[HeldCount] >= 0 AND [AdmittedCount] >= 0 AND [LockedCancelledCount] >= 0");
            t.HasCheckConstraint("CK_DoctorDayQuota_Counts_WithinCapacity", "[HeldCount] + [AdmittedCount] + [LockedCancelledCount] <= [Capacity]");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.DoctorId, x.ServiceDate }).IsUnique();
        builder.HasIndex(x => x.AssignedRoomId);
    }
}
