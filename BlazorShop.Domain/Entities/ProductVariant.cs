namespace BlazorShop.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class ProductVariant
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        public Product? Product { get; set; }

        [MaxLength(64)]
        public string? Sku { get; set; }

        public SizeScale SizeScale { get; set; }

        [MaxLength(16)]
        public string SizeValue { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }

        public int Stock { get; set; }

        [MaxLength(32)]
        public string? Color { get; set; }

        public bool IsDefault { get; set; }
    }
}
