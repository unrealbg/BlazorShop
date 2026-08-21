namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class OrderLineMoneyConfiguration : IEntityTypeConfiguration<OrderLine>
    {
        public void Configure(EntityTypeBuilder<OrderLine> builder)
        {
            builder.ToTable("OrderLines", table =>
            {
                table.HasCheckConstraint("CK_OrderLines_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
                table.HasCheckConstraint("CK_OrderLines_LineTotal_NonNegative", "\"LineTotal\" >= 0");
            });
        }
    }
}
