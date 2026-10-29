using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class MedicalServiceConfiguration : IEntityTypeConfiguration<MedicalService>
{
    public void Configure(EntityTypeBuilder<MedicalService> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(MedicalService), table =>
        {
            table.HasEnumCheck<ServiceCategory>(nameof(MedicalService.ServiceCategory));
        });
        builder.Property(e => e.ServiceCode).HasMaxLength(50);
        builder.Property(e => e.ServiceName).HasMaxLength(200);
        builder.Property(e => e.ServiceCategory).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.ServiceCode).IsUnique();
        builder.HasIndex(e => e.DefaultRoomId); // tham chiếu identity.Room, không FK chéo schema (MOD-01)
    }
}
