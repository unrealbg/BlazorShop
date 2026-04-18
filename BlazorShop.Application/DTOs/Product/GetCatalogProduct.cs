namespace BlazorShop.Application.DTOs.Product
{
    using System.ComponentModel.DataAnnotations;

    public sealed class GetCatalogProduct
    {
        [Required]
        public Guid Id { get; set; }

        public string? Slug { get; set; }

        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Description { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Required]
        public string? Image { get; set; }

        [Required]
        public DateTime CreatedOn { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public string? CategorySlug { get; set; }

        public bool HasVariants { get; set; }
    }
}