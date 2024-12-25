namespace BlazorShop.Application.DTOs.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class CreateOrderItem
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public string? UserId { get; set; }
    }
}
