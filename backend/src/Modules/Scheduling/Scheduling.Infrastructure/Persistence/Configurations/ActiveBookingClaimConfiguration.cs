using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class ActiveBookingClaimConfiguration : IEntityTypeConfiguration<ActiveBookingClaim>
{
    public void Configure(EntityTypeBuilder<ActiveBookingClaim> builder)
    {
        builder.ToTable("ActiveBookingClaim");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClaimSource).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<DoctorDayQuota>().WithMany().HasForeignKey(x => x.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DoctorId, x.ServiceDate, x.PatientId }).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasIndex(x => x.PatientId);
    }
}
