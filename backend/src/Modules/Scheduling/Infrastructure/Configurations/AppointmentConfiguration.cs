using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Scheduling.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Scheduling.Infrastructure.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(Appointment), table =>
        {
            table.HasEnumCheck<AppointmentStatus>(nameof(Appointment.Status));
        });
        builder.Property(e => e.AppointmentNumber).HasMaxLength(50);
        builder.Property(e => e.AppliedConsultationFee).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.CancellationReason).HasMaxLength(500);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.ReservationId).IsUnique();
        builder.HasIndex(e => e.PatientId); // tham chiếu identity.Patient, không FK chéo schema (MOD-01)
        builder.HasIndex(e => e.AppointmentNumber).IsUnique();
        builder.HasIndex(e => e.RescheduledFromAppointmentId).IsUnique().HasFilter("[RescheduledFromAppointmentId] IS NOT NULL");
        builder.HasOne(e => e.Reservation).WithMany().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SessionQuota).WithMany().HasForeignKey(e => e.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RescheduledFromAppointment).WithMany().HasForeignKey(e => e.RescheduledFromAppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
