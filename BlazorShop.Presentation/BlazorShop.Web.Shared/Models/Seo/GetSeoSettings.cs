namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class GetSeoSettings
    {
        public Guid Id { get; set; }

        [MaxLength(SeoValidationConstraints.SiteNameMaxLength)]
        public string? SiteName { get; set; }

        [MaxLength(SeoValidationConstraints.TitleSuffixMaxLength)]
        public string? DefaultTitleSuffix { get; set; }

        [MaxLength(SeoValidationConstraints.MetaDescriptionMaxLength)]
        public string? DefaultMetaDescription { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? DefaultOgImage { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? BaseCanonicalUrl { get; set; }

        [MaxLength(SeoValidationConstraints.CompanyNameMaxLength)]
        public string? CompanyName { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? CompanyLogoUrl { get; set; }

        [MaxLength(SeoValidationConstraints.CompanyPhoneMaxLength)]
        public string? CompanyPhone { get; set; }

        [MaxLength(SeoValidationConstraints.CompanyEmailMaxLength)]
        public string? CompanyEmail { get; set; }

        [MaxLength(SeoValidationConstraints.CompanyAddressMaxLength)]
        public string? CompanyAddress { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? FacebookUrl { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? InstagramUrl { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? XUrl { get; set; }
    }
}