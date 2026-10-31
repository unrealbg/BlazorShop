namespace BlazorShop.Domain.Entities.Identity;

using System.Security.Claims;

// Internal, ephemeral result. Never serialize or log the replacement credential.
public sealed class RefreshRotationResult(string refreshToken, List<Claim> claims)
{
    public string RefreshToken { get; } = refreshToken;
    public List<Claim> Claims { get; } = claims;
}
