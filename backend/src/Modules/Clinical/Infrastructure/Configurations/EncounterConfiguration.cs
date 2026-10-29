using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Clinical.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Clinical.Infrastructure.Configurations;

internal sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Encounter), table =>
        {
            table.HasEnumCheck<EncounterStatus>(nameof(Encounter.Status));
        });
        builder.Property(e => e.SpecialtySnapshot).HasMaxLength(200);
        builder.Property(e => e.DoctorRankSnapshot).HasMaxLength(200);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.AppointmentId).IsUnique();
        // AppointmentId: tham chiếu scheduling.Appointment, không FK chéo schema (MOD-01); đã có index qua PK/UK
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.OriginalDoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.OriginalRoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
    }
}
