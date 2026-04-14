namespace BlazorShop.Infrastructure.Services
{
    using Stripe.Checkout;

    public interface IStripeCheckoutSessionService
    {
        Task<Session> CreateAsync(SessionCreateOptions options, CancellationToken cancellationToken = default);
    }
}