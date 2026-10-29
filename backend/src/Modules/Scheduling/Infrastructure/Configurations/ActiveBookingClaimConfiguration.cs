using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class ActiveBookingClaimConfiguration : IEntityTypeConfiguration<ActiveBookingClaim>
{
    public void Configure(EntityTypeBuilder<ActiveBookingClaim> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(ActiveBookingClaim), table =>
        {
            table.HasEnumCheck<ClaimSource>(nameof(ActiveBookingClaim.ClaimSource));
        });
        builder.Property(e => e.ClaimSource).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.DoctorId); // tham chiếu identity.Doctor, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => new { e.DoctorId, e.ServiceDate, e.PatientId }).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasOne(e => e.SessionQuota).WithMany().HasForeignKey(e => e.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Reservation).WithMany().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Appointment).WithMany().HasForeignKey(e => e.AppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
