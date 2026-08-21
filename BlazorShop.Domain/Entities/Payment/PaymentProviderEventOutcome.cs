namespace BlazorShop.Domain.Entities.Payment
{
    public enum PaymentProviderEventOutcome
    {
        Pending,
        Processed,
        Rejected,
        Ignored,
    }
}
