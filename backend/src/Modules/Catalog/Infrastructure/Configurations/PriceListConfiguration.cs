using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Catalog.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Catalog.Infrastructure.Configurations;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PriceList), table =>
        {
            table.HasEnumCheck<Currency>(nameof(PriceList.Currency));
        });
        builder.Property(e => e.PriceListCode).HasMaxLength(50);
        builder.Property(e => e.PriceListName).HasMaxLength(200);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.PriceListCode).IsUnique();
    }
}
