namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
        {
            builder.ToTable("PaymentTransactions", table =>
            {
                table.HasCheckConstraint(
                    "CK_PaymentTransactions_ExpectedAmountMinor_NonNegative",
                    "\"ExpectedAmountMinor\" >= 0");
                table.HasCheckConstraint(
                    "CK_PaymentTransactions_Currency",
                    "\"Currency\" ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "CK_PaymentTransactions_Lifecycle",
                    "(\"Status\" = 'Pending' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NULL) OR "
                    + "(\"Status\" = 'Paid' AND \"PaidOn\" IS NOT NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NULL) OR "
                    + "(\"Status\" = 'Failed' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NOT NULL AND \"CancelledOn\" IS NULL) OR "
                    + "(\"Status\" = 'Cancelled' AND \"PaidOn\" IS NULL AND \"FailedOn\" IS NULL AND \"CancelledOn\" IS NOT NULL)");
            });

            builder.Property(transaction => transaction.Provider).HasMaxLength(32);
            builder.Property(transaction => transaction.ProviderSessionId).HasMaxLength(255);
            builder.Property(transaction => transaction.ProviderPaymentIntentId).HasMaxLength(255);
            builder.Property(transaction => transaction.Currency).HasMaxLength(3).IsFixedLength();
            builder.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(16);
            builder.Property(transaction => transaction.CreatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(transaction => transaction.UpdatedOn).HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(transaction => new { transaction.OrderId, transaction.Provider }).IsUnique();
            builder.HasIndex(transaction => new { transaction.Provider, transaction.ProviderSessionId })
                .IsUnique()
                .HasFilter("\"ProviderSessionId\" IS NOT NULL");
            builder.HasIndex(transaction => new { transaction.Provider, transaction.ProviderPaymentIntentId })
                .IsUnique()
                .HasFilter("\"ProviderPaymentIntentId\" IS NOT NULL");
            builder.HasIndex(transaction => new { transaction.Status, transaction.UpdatedOn });

            builder.HasOne(transaction => transaction.Order)
                .WithMany(order => order.PaymentTransactions)
                .HasForeignKey(transaction => transaction.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
