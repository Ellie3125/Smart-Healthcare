using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHealthcare.Billing.Domain;
using SmartHealthcare.Shared.Persistence;

namespace SmartHealthcare.Billing.Infrastructure.Configurations;

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.ToTable(nameof(PaymentEvent), table =>
        {
            table.HasEnumCheck<PaymentEventProvider>(nameof(PaymentEvent.Provider));
            table.HasEnumCheck<EventType>(nameof(PaymentEvent.EventType));
            table.HasEnumCheck<PaymentEventStatus>(nameof(PaymentEvent.Status));
        });
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.EventId).HasMaxLength(128);
        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PayloadHash).HasMaxLength(64);
        builder.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => new { e.Provider, e.EventId }).IsUnique().HasDatabaseName("UK_ProviderEvent");
        builder.HasIndex(e => e.PayloadHash);
        builder.HasOne(e => e.PaymentTransaction).WithMany().HasForeignKey(e => e.PaymentTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
