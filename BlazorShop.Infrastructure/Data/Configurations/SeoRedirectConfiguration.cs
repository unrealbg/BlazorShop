namespace BlazorShop.Infrastructure.Data.Configurations
{
    using BlazorShop.Domain.Constants;
    using BlazorShop.Domain.Entities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    internal sealed class SeoRedirectConfiguration : IEntityTypeConfiguration<SeoRedirect>
    {
        public void Configure(EntityTypeBuilder<SeoRedirect> builder)
        {
            builder.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_SeoRedirects_StatusCode",
                    $"\"StatusCode\" IN ({SeoConstraints.PermanentRedirectStatusCode}, {SeoConstraints.TemporaryRedirectStatusCode})"));

            builder.HasIndex(redirect => redirect.OldPath)
                .IsUnique();

            builder.Property(redirect => redirect.OldPath)
                .IsRequired()
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(redirect => redirect.NewPath)
                .IsRequired()
                .HasMaxLength(SeoConstraints.UrlMaxLength);

            builder.Property(redirect => redirect.StatusCode)
                .HasDefaultValue(SeoConstraints.PermanentRedirectStatusCode);

            builder.Property(redirect => redirect.IsActive)
                .HasDefaultValue(true);

            builder.Property(redirect => redirect.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}