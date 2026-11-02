using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Billing.Domain.Entities;

namespace Billing.Infrastructure.Persistence.Configurations;

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("PaymentEvent");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EventId).HasMaxLength(64);
        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PayloadHash).HasMaxLength(128);
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => x.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
        builder.HasIndex(x => x.PayloadHash);
    }
}
