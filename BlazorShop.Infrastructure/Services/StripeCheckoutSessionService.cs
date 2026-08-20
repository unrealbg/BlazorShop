namespace BlazorShop.Infrastructure.Services
{
    using Stripe.Checkout;

    public sealed class StripeCheckoutSessionService : IStripeCheckoutSessionService
    {
        private readonly SessionService _sessionService = new();

        public Task<Session> CreateAsync(
            SessionCreateOptions options,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return _sessionService.CreateAsync(
                options,
                new Stripe.RequestOptions { IdempotencyKey = idempotencyKey },
                cancellationToken);
        }
    }
}
