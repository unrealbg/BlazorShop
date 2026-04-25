namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Domain.Constants;

    public class SeoSettingsDto
    {
        public Guid Id { get; set; }

        [MaxLength(SeoConstraints.SiteNameMaxLength)]
        public string? SiteName { get; set; }

        [MaxLength(SeoConstraints.TitleSuffixMaxLength)]
        public string? DefaultTitleSuffix { get; set; }

        [MaxLength(SeoConstraints.MetaDescriptionMaxLength)]
        public string? DefaultMetaDescription { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? DefaultOgImage { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? BaseCanonicalUrl { get; set; }

        [MaxLength(SeoConstraints.CompanyNameMaxLength)]
        public string? CompanyName { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? CompanyLogoUrl { get; set; }

        [MaxLength(SeoConstraints.CompanyPhoneMaxLength)]
        public string? CompanyPhone { get; set; }

        [MaxLength(SeoConstraints.CompanyEmailMaxLength)]
        public string? CompanyEmail { get; set; }

        [MaxLength(SeoConstraints.CompanyAddressMaxLength)]
        public string? CompanyAddress { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? FacebookUrl { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? InstagramUrl { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? XUrl { get; set; }
    }
}