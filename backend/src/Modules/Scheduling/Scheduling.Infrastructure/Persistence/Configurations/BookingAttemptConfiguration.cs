using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class BookingAttemptConfiguration : IEntityTypeConfiguration<BookingAttempt>
{
    public void Configure(EntityTypeBuilder<BookingAttempt> builder)
    {
        builder.ToTable("BookingAttempt");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CurrentStep).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.AppUserId);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.SelectedDoctorId);
        builder.HasIndex(x => x.SelectedDoctorRankId);
    }
}
