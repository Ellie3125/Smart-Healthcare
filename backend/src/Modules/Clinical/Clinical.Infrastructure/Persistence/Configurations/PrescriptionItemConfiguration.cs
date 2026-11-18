using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItem");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MedicationNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.ActiveIngredientSnapshot).HasMaxLength(200);
        builder.Property(x => x.Dosage).HasMaxLength(200);
        builder.Property(x => x.Route).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Frequency).HasMaxLength(200);
        builder.Property(x => x.UsageInstructions).HasColumnType("nvarchar(max)");

        builder.HasOne<Prescription>().WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MedicationId);
    }
}
