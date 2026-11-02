using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Clinical.Domain.Entities;

namespace Clinical.Infrastructure.Persistence.Configurations;

internal sealed class ServiceOrderItemConfiguration : IEntityTypeConfiguration<ServiceOrderItem>
{
    public void Configure(EntityTypeBuilder<ServiceOrderItem> builder)
    {
        builder.ToTable("ServiceOrderItem");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ServiceNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.AppliedPrice).HasPrecision(18, 2);
        builder.Property(x => x.ItemStatus).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<ServiceOrder>().WithMany().HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MedicalServiceId);
    }
}
