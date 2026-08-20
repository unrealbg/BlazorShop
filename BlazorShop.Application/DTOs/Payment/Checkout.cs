namespace BlazorShop.Application.DTOs.Payment
{
    using System.ComponentModel.DataAnnotations;
    using BlazorShop.Domain.Contracts.Payment;

    public class Checkout
    {
        [Required]
        public required Guid PaymentMethodId { get; set; }

        [Required]
        public required IEnumerable<CartLineRequest> Carts { get; set; }
    }
}
