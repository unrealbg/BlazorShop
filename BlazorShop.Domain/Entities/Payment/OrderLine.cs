namespace BlazorShop.Domain.Entities.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class OrderLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public string ProductNameSnapshot { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? SkuSnapshot { get; set; }

        [MaxLength(32)]
        public string? SizeScaleSnapshot { get; set; }

        [MaxLength(16)]
        public string? SizeValueSnapshot { get; set; }

        [MaxLength(32)]
        public string? ColorSnapshot { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }

        public Order? Order { get; set; }
    }
}
