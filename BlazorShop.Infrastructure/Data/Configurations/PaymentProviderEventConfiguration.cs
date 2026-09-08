namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class PaymentProviderEventConfiguration : IEntityTypeConfiguration<PaymentProviderEvent>
    {
        public void Configure(EntityTypeBuilder<PaymentProviderEvent> builder)
        {
            builder.ToTable("PaymentProviderEvents");

            builder.Property(providerEvent => providerEvent.Provider).HasMaxLength(32);
            builder.Property(providerEvent => providerEvent.ProviderEventId).HasMaxLength(255);
            builder.Property(providerEvent => providerEvent.EventType).HasMaxLength(128);
            builder.Property(providerEvent => providerEvent.ProcessingOutcome)
                .HasConversion<string>()
                .HasMaxLength(16);
            builder.Property(providerEvent => providerEvent.FailureReason).HasMaxLength(1000);
            builder.Property(providerEvent => providerEvent.ReceivedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(providerEvent => new { providerEvent.Provider, providerEvent.ProviderEventId })
                .IsUnique();
            builder.HasIndex(providerEvent => providerEvent.PaymentTransactionId);
            builder.HasIndex(providerEvent => providerEvent.OrderId);
            builder.HasIndex(providerEvent => new { providerEvent.ProcessingOutcome, providerEvent.ReceivedOn });

            builder.HasOne(providerEvent => providerEvent.PaymentTransaction)
                .WithMany(transaction => transaction.ProviderEvents)
                .HasForeignKey(providerEvent => providerEvent.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(providerEvent => providerEvent.Order)
                .WithMany()
                .HasForeignKey(providerEvent => providerEvent.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
