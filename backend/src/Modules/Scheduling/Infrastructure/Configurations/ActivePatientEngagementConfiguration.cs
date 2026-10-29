using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class ActivePatientEngagementConfiguration : IEntityTypeConfiguration<ActivePatientEngagement>
{
    public void Configure(EntityTypeBuilder<ActivePatientEngagement> builder)
    {
        builder.HasKey(e => e.PatientId);
        builder.Property(e => e.PatientId).ValueGeneratedNever();
        // PatientId: tham chiếu identity.Patient, không FK chéo schema (MOD-01); đã có index qua PK/UK
        builder.HasIndex(e => e.EncounterId); // tham chiếu clinical.Encounter, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.CurrentStationId).IsUnique();
        builder.HasIndex(e => e.CurrentTicketId).IsUnique();
        builder.HasOne(e => e.CurrentStation).WithMany().HasForeignKey(e => e.CurrentStationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CurrentTicket).WithMany().HasForeignKey(e => e.CurrentTicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
