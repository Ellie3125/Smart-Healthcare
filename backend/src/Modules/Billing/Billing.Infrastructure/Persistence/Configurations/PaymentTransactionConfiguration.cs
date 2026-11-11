using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransaction");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionReference).HasMaxLength(64);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(64);
        builder.Property(x => x.TransactionAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(64);
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ProviderRawData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<PaymentIntent>().WithMany().HasForeignKey(x => x.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TransactionReference).IsUnique();
        builder.HasIndex(x => x.ProviderTransactionId).IsUnique().HasFilter("[ProviderTransactionId] IS NOT NULL");
    }
}
