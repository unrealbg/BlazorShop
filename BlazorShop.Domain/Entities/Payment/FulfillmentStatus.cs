namespace BlazorShop.Domain.Entities.Payment
{
    public enum FulfillmentStatus
    {
        NotStarted,
        Shipped,
        InTransit,
        OutForDelivery,
        Delivered,
    }
}
