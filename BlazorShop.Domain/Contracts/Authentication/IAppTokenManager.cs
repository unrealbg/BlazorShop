namespace BlazorShop.Domain.Contracts.Authentication
{
    using System.Security.Claims;

    public interface IAppTokenManager
    {
        string GetReFreshToken();

        List<Claim> GetUserClaimsFromToken(string token);

        Task<bool> ValidateRefreshTokenAsync(string refreshToken);

        Task<string> GetUserIdByRefreshTokenAsync(string refreshToken);

        Task<int> AddRefreshTokenAsync(string userId, string refreshToken);

        Task<int> UpdateRefreshTokenAsync(string userId, string refreshToken);

        string GenerateAccessToken(List<Claim> claims);
    }
}
