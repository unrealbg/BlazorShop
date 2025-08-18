namespace BlazorShop.Application.DTOs.Product.ProductVariant
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateProductVariant
    {
        [Required]
        public Guid Id { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [MaxLength(64)]
        public string? Sku { get; set; }

        [Required]
        public int SizeScale { get; set; }

        [Required]
        [MaxLength(16)]
        public string SizeValue { get; set; } = string.Empty;

        public decimal? Price { get; set; }

        public int Stock { get; set; }

        [MaxLength(32)]
        public string? Color { get; set; }

        public bool IsDefault { get; set; }
    }
}
