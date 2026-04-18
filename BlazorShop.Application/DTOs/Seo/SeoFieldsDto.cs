namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Domain.Constants;

    public class SeoFieldsDto
    {
        [MaxLength(SeoConstraints.SlugMaxLength)]
        public string? Slug { get; set; }

        [MaxLength(SeoConstraints.MetaTitleMaxLength)]
        public string? MetaTitle { get; set; }

        [MaxLength(SeoConstraints.MetaDescriptionMaxLength)]
        public string? MetaDescription { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? CanonicalUrl { get; set; }

        [MaxLength(SeoConstraints.MetaTitleMaxLength)]
        public string? OgTitle { get; set; }

        [MaxLength(SeoConstraints.MetaDescriptionMaxLength)]
        public string? OgDescription { get; set; }

        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? OgImage { get; set; }

        public bool RobotsIndex { get; set; } = true;

        public bool RobotsFollow { get; set; } = true;

        public string? SeoContent { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}