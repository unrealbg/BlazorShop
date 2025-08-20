namespace BlazorShop.Domain.Entities.Payment
{
    public class OrderLine
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public Guid ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public Order? Order { get; set; }
    }
}
