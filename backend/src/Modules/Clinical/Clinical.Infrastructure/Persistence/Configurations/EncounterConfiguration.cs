using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounter");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SpecialtySnapshot).HasMaxLength(200);
        builder.Property(x => x.DoctorRankSnapshot).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.AppointmentId).IsUnique();
        builder.HasIndex(x => new { x.PatientId, x.CheckedInAtUtc });
        builder.HasIndex(x => x.OriginalDoctorId);
        builder.HasIndex(x => x.OriginalRoomId);
    }
}
