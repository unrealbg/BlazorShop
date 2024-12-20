namespace BlazorShop.Web.Shared.Models.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class CreateOrderItem : ProcessCart
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}
