namespace BlazorShop.Application.DTOs.Payment
{
    public sealed record StripeCheckoutInitialization(
        int Version,
        Guid OrderId,
        string OrderReference,
        Guid PaymentTransactionId,
        long ExpectedAmountMinor,
        string Currency,
        IReadOnlyList<string> PaymentMethodTypes,
        string Mode,
        IReadOnlyList<StripeCheckoutLineItem> Lines,
        string SuccessUrl,
        string CancelUrl);

    public sealed record StripeCheckoutLineItem(
        Guid ProductId,
        Guid? ProductVariantId,
        string ProductName,
        string? Description,
        int Quantity,
        long UnitAmount,
        string Currency);

    public sealed record CheckoutProviderInitialization(
        DateTime StartedOn,
        StripeCheckoutInitialization Initialization);
}
