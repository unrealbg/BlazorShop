namespace BlazorShop.Domain.Entities.Payment
{
    public enum CheckoutIdempotencyState
    {
        Processing,
        LocalCommitted,
        Completed,
        Failed,
    }
}
