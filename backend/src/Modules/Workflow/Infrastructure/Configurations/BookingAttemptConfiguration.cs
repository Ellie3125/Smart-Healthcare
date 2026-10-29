using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Workflow.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Workflow.Infrastructure.Configurations;

internal sealed class BookingAttemptConfiguration : IEntityTypeConfiguration<BookingAttempt>
{
    public void Configure(EntityTypeBuilder<BookingAttempt> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(BookingAttempt), table =>
        {
            table.HasEnumCheck<CurrentStep>(nameof(BookingAttempt.CurrentStep));
            table.HasEnumCheck<BookingAttemptStatus>(nameof(BookingAttempt.Status));
        });
        builder.Property(e => e.CurrentStep).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.AppUserId); // tham chiếu identity.AppUser, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.SelectedDoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.SelectedDoctorRankId); // tham chiếu catalog.DoctorRank, không FK chéo schema (MOD-01)
    }
}
