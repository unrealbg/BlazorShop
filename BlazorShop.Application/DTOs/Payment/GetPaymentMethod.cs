namespace BlazorShop.Application.DTOs.Payment
{
    public class GetPaymentMethod
    {
        public required Guid Id { get; set; }

        public required string Name { get; set; }
    }
}