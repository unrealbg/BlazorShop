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

        public Task<Session> GetAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return _sessionService.GetAsync(sessionId, cancellationToken: cancellationToken);
        }

        public Task<Session> ExpireAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            return _sessionService.ExpireAsync(sessionId, cancellationToken: cancellationToken);
        }
    }
}
