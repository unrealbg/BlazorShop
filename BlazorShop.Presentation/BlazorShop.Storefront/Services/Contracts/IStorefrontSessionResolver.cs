namespace BlazorShop.Storefront.Services.Contracts
{
    public interface IStorefrontSessionResolver
    {
        Task<StorefrontSessionInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    }

    public sealed record StorefrontSessionInfo(bool IsAuthenticated, bool IsAdmin, string? DisplayName, string? Email)
    {
        public static StorefrontSessionInfo Anonymous { get; } = new(false, false, null, null);
    }
}