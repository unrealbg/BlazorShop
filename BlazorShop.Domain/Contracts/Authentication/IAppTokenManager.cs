namespace BlazorShop.Domain.Contracts.Authentication
{
    using System.Security.Claims;

    using BlazorShop.Domain.Entities.Identity;

    public interface IAppTokenManager
    {
        string GetReFreshToken();

        RefreshToken CreateRefreshToken(string userId, string refreshToken, string? createdByIp = null, string? userAgent = null);

        List<Claim> GetUserClaimsFromToken(string token);

        Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);

        Task<bool> ValidateRefreshTokenAsync(string refreshToken);

        bool IsRefreshTokenActive(RefreshToken refreshToken);

        Task<int> AddRefreshTokenAsync(RefreshToken refreshToken);

        Task<int> RevokeRefreshTokenAsync(string refreshToken, string? revokedByIp = null, string? replacedByRefreshToken = null);

        Task<int> RevokeRefreshTokenFamilyAsync(string refreshToken, string? revokedByIp = null);

        string GenerateAccessToken(List<Claim> claims);
    }
}
