using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PaymentTransaction), table =>
        {
            table.HasEnumCheck<Currency>(nameof(PaymentTransaction.Currency));
            table.HasEnumCheck<PaymentStatus>(nameof(PaymentTransaction.PaymentStatus));
        });
        builder.Property(e => e.TransactionReference).HasMaxLength(128);
        builder.Property(e => e.ProviderTransactionId).HasMaxLength(128);
        builder.Property(e => e.TransactionAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ProviderRawData).HasColumnType("nvarchar(max)");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.TransactionReference).IsUnique();
        builder.HasIndex(e => e.ProviderTransactionId).IsUnique().HasFilter("[ProviderTransactionId] IS NOT NULL");
        builder.HasOne(e => e.PaymentIntent).WithMany().HasForeignKey(e => e.PaymentIntentId).OnDelete(DeleteBehavior.Restrict);
    }
}
