namespace BlazorShop.Application.DTOs.Product
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product.ProductVariant;

    public class GetProduct : ProductBase
    {
        [Required]
        public Guid Id { get; set; }

        public GetCategory? Category { get; set; }

        public DateTime CreatedOn { get; set; }

        public IEnumerable<GetProductVariant> Variants { get; set; } = Array.Empty<GetProductVariant>();
    }
}
