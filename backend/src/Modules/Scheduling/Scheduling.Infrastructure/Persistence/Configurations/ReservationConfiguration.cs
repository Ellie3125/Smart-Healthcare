using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservation");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BookingChannel).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<DoctorDayQuota>().WithMany().HasForeignKey(x => x.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BookingAttempt>().WithMany().HasForeignKey(x => x.BookingAttemptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.AppUserId);
    }
}
