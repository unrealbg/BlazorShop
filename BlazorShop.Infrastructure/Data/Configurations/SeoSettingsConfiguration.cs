namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Constants;
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class SeoSettingsConfiguration : IEntityTypeConfiguration<SeoSettings>
    {
        public void Configure(EntityTypeBuilder<SeoSettings> builder)
        {
            builder.Property(settings => settings.SiteName)
                .HasMaxLength(SeoConstraints.SiteNameMaxLength);

            builder.Property(settings => settings.DefaultTitleSuffix)
                .HasMaxLength(SeoConstraints.TitleSuffixMaxLength);

            builder.Property(settings => settings.DefaultMetaDescription)
                .HasMaxLength(SeoConstraints.MetaDescriptionMaxLength);

            builder.Property(settings => settings.DefaultOgImage)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(settings => settings.BaseCanonicalUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(settings => settings.CompanyName)
                .HasMaxLength(SeoConstraints.CompanyNameMaxLength);

            builder.Property(settings => settings.CompanyLogoUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(settings => settings.CompanyPhone)
                .HasMaxLength(SeoConstraints.CompanyPhoneMaxLength);

            builder.Property(settings => settings.CompanyEmail)
                .HasMaxLength(SeoConstraints.CompanyEmailMaxLength);

            builder.Property(settings => settings.CompanyAddress)
                .HasMaxLength(SeoConstraints.CompanyAddressMaxLength);

            builder.Property(settings => settings.FacebookUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(settings => settings.InstagramUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(settings => settings.XUrl)
                .HasMaxLength(SeoConstraints.UrlMaxLength);
        }
    }
}