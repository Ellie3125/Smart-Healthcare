using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Reservation), table =>
        {
            table.HasEnumCheck<BookingChannel>(nameof(Reservation.BookingChannel));
            table.HasEnumCheck<ReservationStatus>(nameof(Reservation.Status));
        });
        builder.Property(e => e.BookingChannel).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(128);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.BookingAttemptId); // tham chiếu workflow.BookingAttempt, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.AppUserId); // tham chiếu identity.AppUser, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.IdempotencyKey).IsUnique();
        builder.HasOne(e => e.SessionQuota).WithMany().HasForeignKey(e => e.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
    }
}
