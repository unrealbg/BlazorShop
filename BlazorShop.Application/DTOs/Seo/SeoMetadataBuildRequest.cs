namespace BlazorShop.Application.DTOs.Seo
{
    public class SeoMetadataBuildRequest
    {
        public string? PageTitle { get; set; }

        public string? RelativePath { get; set; }

        public bool SuppressCanonicalUrl { get; set; }

        public bool SuppressOpenGraph { get; set; }

        public SeoFieldsDto? PageSeo { get; set; }

        public SeoSettingsDto? Settings { get; set; }
    }
}