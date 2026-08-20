namespace BlazorShop.Web.Shared.Models.Payment
{
    public sealed record CheckoutAttempt(Guid IdempotencyKey, string IntentSignature);
}
