namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Constants;
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasIndex(category => category.Slug)
                .IsUnique();

            builder.Property(category => category.Slug)
                .HasMaxLength(SeoConstraints.SlugMaxLength);

            builder.Property(category => category.MetaTitle)
                .HasMaxLength(SeoConstraints.MetaTitleMaxLength);

            builder.Property(category => category.MetaDescription)
                .HasMaxLength(SeoConstraints.MetaDescriptionMaxLength);

            builder.Property(category => category.CanonicalUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(category => category.OgTitle)
                .HasMaxLength(SeoConstraints.MetaTitleMaxLength);

            builder.Property(category => category.OgDescription)
                .HasMaxLength(SeoConstraints.MetaDescriptionMaxLength);

            builder.Property(category => category.OgImage)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(category => category.RobotsIndex)
                .HasDefaultValue(true);

            builder.Property(category => category.RobotsFollow)
                .HasDefaultValue(true);

            builder.Property(category => category.SeoContent)
                .HasColumnType("text");

            builder.Property(category => category.IsPublished)
                .HasDefaultValue(true);
        }
    }
}