using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class ActiveStationAllocationConfiguration : IEntityTypeConfiguration<ActiveStationAllocation>
{
    public void Configure(EntityTypeBuilder<ActiveStationAllocation> builder)
    {
        builder.HasKey(e => e.StationId);
        builder.Property(e => e.StationId).ValueGeneratedNever();
        builder.HasIndex(e => e.CurrentPatientId).IsUnique();
        // CurrentPatientId: tham chiếu identity.Patient, không FK chéo schema (MOD-01); đã có index qua PK/UK
        builder.HasIndex(e => e.CurrentTicketId).IsUnique();
        builder.HasOne(e => e.Station).WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CurrentTicket).WithMany().HasForeignKey(e => e.CurrentTicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
