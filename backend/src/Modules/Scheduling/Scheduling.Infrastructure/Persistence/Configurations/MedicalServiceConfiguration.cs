using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduling.Domain.Entities;

namespace Scheduling.Infrastructure.Persistence.Configurations;

internal sealed class MedicalServiceConfiguration : IEntityTypeConfiguration<MedicalService>
{
    public void Configure(EntityTypeBuilder<MedicalService> builder)
    {
        builder.ToTable("MedicalService");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServiceCode).HasMaxLength(64);
        builder.Property(x => x.ServiceName).HasMaxLength(200);
        builder.Property(x => x.ServiceCategory).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.ServiceCode).IsUnique();
        builder.HasIndex(x => x.DefaultRoomId);
    }
}
