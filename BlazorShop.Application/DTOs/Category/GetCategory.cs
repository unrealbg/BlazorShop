namespace BlazorShop.Application.DTOs.Category
{
    using BlazorShop.Application.DTOs.Product;

    public class GetCategory : CategoryBase
    {
        public Guid Id { get; set; }

        public string? Slug { get; set; }

        public string? MetaTitle { get; set; }

        public string? MetaDescription { get; set; }

        public string? CanonicalUrl { get; set; }

        public string? OgTitle { get; set; }

        public string? OgDescription { get; set; }

        public string? OgImage { get; set; }

        public string? SeoContent { get; set; }

        public bool RobotsIndex { get; set; } = true;

        public bool RobotsFollow { get; set; } = true;

        public ICollection<GetProduct>? Products { get; set; }
    }
}