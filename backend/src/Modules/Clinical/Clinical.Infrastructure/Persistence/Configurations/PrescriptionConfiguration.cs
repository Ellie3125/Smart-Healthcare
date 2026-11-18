using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescription");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PrescriptionCode).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.GeneralInstructions).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<Encounter>().WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PrescriptionCode).IsUnique();
        builder.HasIndex(x => x.PrescribedByDoctorId);
    }
}
