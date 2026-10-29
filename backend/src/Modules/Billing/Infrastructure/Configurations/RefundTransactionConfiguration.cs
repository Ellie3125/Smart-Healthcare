using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(RefundTransaction), table =>
        {
            table.HasEnumCheck<Currency>(nameof(RefundTransaction.Currency));
            table.HasEnumCheck<RefundTransactionStatus>(nameof(RefundTransaction.Status));
        });
        builder.Property(e => e.ProviderRefundTransactionId).HasMaxLength(128);
        builder.Property(e => e.RefundedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.ProviderResponsePayload).HasColumnType("nvarchar(max)");
        builder.HasIndex(e => e.RefundRequestId).IsUnique();
        builder.HasIndex(e => e.ProviderRefundTransactionId).IsUnique().HasFilter("[ProviderRefundTransactionId] IS NOT NULL");
        builder.HasOne(e => e.RefundRequest).WithMany().HasForeignKey(e => e.RefundRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OriginalPaymentTransaction).WithMany().HasForeignKey(e => e.OriginalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
