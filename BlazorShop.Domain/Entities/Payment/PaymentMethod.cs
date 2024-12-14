namespace BlazorShop.Domain.Entities.Payment
{
    using System.ComponentModel.DataAnnotations;

    public class PaymentMethod
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;
    }
}
