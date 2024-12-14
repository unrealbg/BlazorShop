namespace BlazorShop.Application.DTOs.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class Checkout
    {
        [Required]
        public required Guid PaymentMethodId { get; set; }

        [Required]
        public required IEnumerable<ProcessCart> Carts { get; set; }
    }
}
