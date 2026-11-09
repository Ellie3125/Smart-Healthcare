using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointment");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AppointmentNumber).HasMaxLength(64);
        builder.Property(x => x.AppliedConsultationFee).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.CancellationReason).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorDayQuota>().WithMany().HasForeignKey(x => x.SessionQuotaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(x => x.RescheduledFromAppointmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReservationId).IsUnique();
        builder.HasIndex(x => x.AppointmentNumber).IsUnique();
        builder.HasIndex(x => x.PatientId);
    }
}
