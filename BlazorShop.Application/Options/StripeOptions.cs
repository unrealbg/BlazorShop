namespace BlazorShop.Application.Options
{
    public sealed class StripeOptions
    {
        public const string SectionName = "Stripe";

        public string SecretKey { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;
    }
}
