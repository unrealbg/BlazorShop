namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Seo;

    public class StorefrontSeoMetadata
    {
        public string? Title { get; set; }

        public string? MetaDescription { get; set; }

        public string? CanonicalUrl { get; set; }

        public string? OgTitle { get; set; }

        public string? OgDescription { get; set; }

        public string? OgImage { get; set; }

        public string? SiteName { get; set; }

        public bool RobotsIndex { get; set; } = true;

        public bool RobotsFollow { get; set; } = true;
    }

    public class StorefrontSeoPageData
    {
        public string? MetaTitle { get; set; }

        public string? MetaDescription { get; set; }

        public string? CanonicalUrl { get; set; }

        public string? OgTitle { get; set; }

        public string? OgDescription { get; set; }

        public string? OgImage { get; set; }

        public bool? RobotsIndex { get; set; }

        public bool? RobotsFollow { get; set; }

        public static StorefrontSeoPageData? FromCategory(GetCategory? category)
        {
            if (category is null)
            {
                return null;
            }

            return new StorefrontSeoPageData
            {
                MetaTitle = category.MetaTitle,
                MetaDescription = category.MetaDescription,
                CanonicalUrl = category.CanonicalUrl,
                OgTitle = category.OgTitle,
                OgDescription = category.OgDescription,
                OgImage = category.OgImage,
                RobotsIndex = category.RobotsIndex,
                RobotsFollow = category.RobotsFollow,
            };
        }
    }

    public class StorefrontSeoMetadataBuildRequest
    {
        public string? PageTitle { get; set; }

        public string? RelativePath { get; set; }

        public string? FallbackMetaDescription { get; set; }

        public string? FallbackOgImage { get; set; }

        public StorefrontSeoPageData? PageSeo { get; set; }

        public GetSeoSettings? Settings { get; set; }
    }
}