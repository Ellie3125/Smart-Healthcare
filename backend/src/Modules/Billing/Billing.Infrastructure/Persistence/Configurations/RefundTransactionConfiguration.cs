using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("RefundTransaction");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderRefundTransactionId).HasMaxLength(64);
        builder.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ProviderResponsePayload).HasColumnType("nvarchar(max)");

        builder.HasOne<RefundRequest>().WithMany().HasForeignKey(x => x.RefundRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.OriginalPaymentTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RefundRequestId).IsUnique();
        builder.HasIndex(x => x.ProviderRefundTransactionId).IsUnique().HasFilter("[ProviderRefundTransactionId] IS NOT NULL");
    }
}
