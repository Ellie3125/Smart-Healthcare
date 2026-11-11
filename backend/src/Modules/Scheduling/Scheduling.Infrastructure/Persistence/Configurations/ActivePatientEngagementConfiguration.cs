using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class ActivePatientEngagementConfiguration : IEntityTypeConfiguration<ActivePatientEngagement>
{
    public void Configure(EntityTypeBuilder<ActivePatientEngagement> builder)
    {
        builder.ToTable("ActivePatientEngagement");
        builder.HasKey(x => x.PatientId);
        builder.Property(x => x.PatientId).ValueGeneratedNever();

        builder.HasOne<ServiceStation>().WithMany().HasForeignKey(x => x.CurrentStationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QueueTicket>().WithMany().HasForeignKey(x => x.CurrentTicketId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CurrentStationId).IsUnique();
        builder.HasIndex(x => x.CurrentTicketId).IsUnique();
        builder.HasIndex(x => x.EncounterId);
    }
}
