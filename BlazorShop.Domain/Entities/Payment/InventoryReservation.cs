namespace BlazorShop.Domain.Entities.Payment
{
    public class InventoryReservation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public int Quantity { get; set; }

        public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Reserved;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? ConsumedOn { get; set; }

        public DateTime? ReleasedOn { get; set; }

        public Order? Order { get; set; }
    }
}
