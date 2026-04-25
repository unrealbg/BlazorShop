namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Constants;
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasIndex(product => new { product.CategoryId, product.CreatedOn });
            builder.HasIndex(product => product.Slug)
                .IsUnique();

            builder.Property(product => product.Slug)
                .HasMaxLength(SeoConstraints.SlugMaxLength);

            builder.Property(product => product.MetaTitle)
                .HasMaxLength(SeoConstraints.MetaTitleMaxLength);

            builder.Property(product => product.MetaDescription)
                .HasMaxLength(SeoConstraints.MetaDescriptionMaxLength);

            builder.Property(product => product.CanonicalUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(product => product.OgTitle)
                .HasMaxLength(SeoConstraints.MetaTitleMaxLength);

            builder.Property(product => product.OgDescription)
                .HasMaxLength(SeoConstraints.MetaDescriptionMaxLength);

            builder.Property(product => product.OgImage)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(product => product.RobotsIndex)
                .HasDefaultValue(true);

            builder.Property(product => product.RobotsFollow)
                .HasDefaultValue(true);

            builder.Property(product => product.SeoContent)
                .HasColumnType("text");

            builder.Property(product => product.IsPublished)
                .HasDefaultValue(true);
        }
    }
}