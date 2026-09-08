namespace BlazorShop.Infrastructure.Services
{
    using Stripe.Checkout;

    public interface IStripeCheckoutSessionService
    {
        Task<Session> CreateAsync(
            SessionCreateOptions options,
            string idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<Session> GetAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        Task<Session> ExpireAsync(
            string sessionId,
            CancellationToken cancellationToken = default);
    }
}
