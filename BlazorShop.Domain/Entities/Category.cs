namespace BlazorShop.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;

    public class Category
    {
        [Key]
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? Slug { get; set; }

        public string? MetaTitle { get; set; }

        public string? MetaDescription { get; set; }

        public string? CanonicalUrl { get; set; }

        public string? OgTitle { get; set; }

        public string? OgDescription { get; set; }

        public string? OgImage { get; set; }

        public bool RobotsIndex { get; set; } = true;

        public bool RobotsFollow { get; set; } = true;

        public string? SeoContent { get; set; }

        public bool IsPublished { get; set; } = true;

        public ICollection<Product>? Products { get; set; }
    }
}
