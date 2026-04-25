namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class SeoFieldsBase
    {
        [MaxLength(SeoValidationConstraints.SlugMaxLength)]
        public string? Slug { get; set; }

        [MaxLength(SeoValidationConstraints.MetaTitleMaxLength)]
        public string? MetaTitle { get; set; }

        [MaxLength(SeoValidationConstraints.MetaDescriptionMaxLength)]
        public string? MetaDescription { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? CanonicalUrl { get; set; }

        [MaxLength(SeoValidationConstraints.MetaTitleMaxLength)]
        public string? OgTitle { get; set; }

        [MaxLength(SeoValidationConstraints.MetaDescriptionMaxLength)]
        public string? OgDescription { get; set; }

        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? OgImage { get; set; }

        public bool RobotsIndex { get; set; } = true;

        public bool RobotsFollow { get; set; } = true;

        public string? SeoContent { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}