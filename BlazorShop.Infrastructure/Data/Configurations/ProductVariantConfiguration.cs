namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
    {
        public void Configure(EntityTypeBuilder<ProductVariant> builder)
        {
            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_ProductVariants_Stock_NonNegative",
                    "\"Stock\" >= 0"));

            builder.Property(variant => variant.Stock)
                .IsConcurrencyToken();
        }
    }
}
