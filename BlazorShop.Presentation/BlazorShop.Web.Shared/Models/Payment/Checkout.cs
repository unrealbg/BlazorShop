namespace BlazorShop.Web.Shared.Models.Payment
{
    using System.ComponentModel.DataAnnotations;
    using BlazorShop.Domain.Contracts.Payment;

    public class Checkout
    {
        [Required]
        public Guid PaymentMethodId { get; set; }

        [Required]
        public IEnumerable<CartLineRequest> Carts { get; set; } = [];
    }
}
