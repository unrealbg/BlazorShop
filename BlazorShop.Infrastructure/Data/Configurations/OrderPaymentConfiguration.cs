namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class OrderPaymentConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders", table =>
            {
                table.HasCheckConstraint("CK_Orders_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("CK_Orders_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
                table.HasCheckConstraint("CK_Orders_SubtotalAmount_NonNegative", "\"SubtotalAmount\" >= 0");
                table.HasCheckConstraint("CK_Orders_DiscountAmount_NonNegative", "\"DiscountAmount\" >= 0");
                table.HasCheckConstraint("CK_Orders_ShippingAmount_NonNegative", "\"ShippingAmount\" >= 0");
                table.HasCheckConstraint("CK_Orders_TaxAmount_NonNegative", "\"TaxAmount\" >= 0");
                table.HasCheckConstraint(
                    "CK_Orders_TotalAmount_Components",
                    "\"TotalAmount\" = \"SubtotalAmount\" - \"DiscountAmount\" + \"ShippingAmount\" + \"TaxAmount\"");
                table.HasCheckConstraint(
                    "CK_Orders_OrderStatus",
                    "\"OrderStatus\" IN ('Pending', 'Confirmed', 'Completed', 'Cancelled')");
                table.HasCheckConstraint(
                    "CK_Orders_PaymentStatus",
                    "\"PaymentStatus\" IN ('Pending', 'Paid', 'Failed', 'Cancelled')");
                table.HasCheckConstraint(
                    "CK_Orders_PaymentMethod",
                    "\"PaymentMethod\" IN ('Unknown', 'CashOnDelivery', 'BankTransfer', 'Stripe')");
                table.HasCheckConstraint(
                    "CK_Orders_FulfillmentStatus",
                    "\"FulfillmentStatus\" IN ('NotStarted', 'Shipped', 'InTransit', 'OutForDelivery', 'Delivered')");
            });

            builder.Property(order => order.Currency).HasMaxLength(3).IsFixedLength();
            builder.Property(order => order.OrderStatus).HasConversion<string>().HasMaxLength(16);
            builder.Property(order => order.PaymentStatus).HasConversion<string>().HasMaxLength(16);
            builder.Property(order => order.PaymentMethod).HasConversion<string>().HasMaxLength(24);
            builder.Property(order => order.FulfillmentStatus).HasConversion<string>().HasMaxLength(24);
            builder.Property(order => order.LegacyStatus).HasMaxLength(64);
            builder.Property(order => order.LegacyShippingStatus).HasMaxLength(64);
            builder.Property(order => order.CustomerNameSnapshot).HasMaxLength(256);
            builder.Property(order => order.CustomerEmailSnapshot).HasMaxLength(256);
            builder.Property(order => order.ShippingAddressSnapshot).HasMaxLength(2000);
            builder.Property(order => order.BillingAddressSnapshot).HasMaxLength(2000);
            builder.Property(order => order.Version).IsRowVersion();

            builder.HasIndex(order => new { order.OrderStatus, order.CreatedOn });
            builder.HasIndex(order => new { order.PaymentStatus, order.CreatedOn });
            builder.HasIndex(order => new { order.FulfillmentStatus, order.CreatedOn });
        }
    }
}
