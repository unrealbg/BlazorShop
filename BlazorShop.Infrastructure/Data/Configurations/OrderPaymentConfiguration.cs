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
            });

            builder.Property(order => order.Currency).HasMaxLength(3).IsFixedLength();
        }
    }
}
