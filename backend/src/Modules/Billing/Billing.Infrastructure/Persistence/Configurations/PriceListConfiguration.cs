using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("PriceList");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PriceListCode).HasMaxLength(64);
        builder.Property(x => x.PriceListName).HasMaxLength(200);
        builder.Property(x => x.Currency).HasMaxLength(64);

        builder.HasIndex(x => x.PriceListCode).IsUnique();
    }
}
