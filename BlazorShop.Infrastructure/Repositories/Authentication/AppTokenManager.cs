namespace BlazorShop.Infrastructure.Repositories.Authentication
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Net;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;

    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.IdentityModel.Tokens;

    public class AppTokenManager : IAppTokenManager
    {
        private const int RefreshTokenLifetimeDaysDefault = 14;

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AppTokenManager(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public string GetReFreshToken()
        {
            const int byteSize = 64;
            byte[] randomBytes = new byte[byteSize];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            string token = Convert.ToBase64String(randomBytes);

            return WebUtility.UrlEncode(token);
        }

        public RefreshToken CreateRefreshToken(string userId, string refreshToken, string? createdByIp = null, string? userAgent = null)
        {
            var createdAtUtc = DateTime.UtcNow;

            return new RefreshToken
            {
                UserId = userId,
                TokenHash = HashRefreshToken(refreshToken),
                CreatedAtUtc = createdAtUtc,
                ExpiresAtUtc = createdAtUtc.AddDays(GetRefreshTokenLifetimeDays()),
                CreatedByIp = createdByIp,
                UserAgent = userAgent,
            };
        }

        public List<Claim> GetUserClaimsFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            return jwtToken is not null ? jwtToken.Claims.ToList() : [];
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var refreshTokenHash = HashRefreshToken(refreshToken);
            return await _context.RefreshTokens.FirstOrDefaultAsync(_ => _.TokenHash == refreshTokenHash);
        }

        public async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
        {
            var storedRefreshToken = await GetRefreshTokenAsync(refreshToken);
            return storedRefreshToken is not null && IsRefreshTokenActive(storedRefreshToken);
        }

        public bool IsRefreshTokenActive(RefreshToken refreshToken)
        {
            return refreshToken.RevokedAtUtc is null && refreshToken.ExpiresAtUtc > DateTime.UtcNow;
        }

        public async Task<int> AddRefreshTokenAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);

            return await _context.SaveChangesAsync();
        }

        public async Task<int> RevokeRefreshTokenAsync(string refreshToken, string? revokedByIp = null, string? replacedByRefreshToken = null)
        {
            var storedRefreshToken = await GetRefreshTokenAsync(refreshToken);

            if (storedRefreshToken is null)
            {
                return 0;
            }

            var hasChanges = false;

            if (storedRefreshToken.RevokedAtUtc is null)
            {
                storedRefreshToken.RevokedAtUtc = DateTime.UtcNow;
                storedRefreshToken.RevokedByIp = revokedByIp;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(replacedByRefreshToken)
                && string.IsNullOrWhiteSpace(storedRefreshToken.ReplacedByTokenHash))
            {
                storedRefreshToken.ReplacedByTokenHash = HashRefreshToken(replacedByRefreshToken);
                hasChanges = true;
            }

            return hasChanges ? await _context.SaveChangesAsync() : 0;
        }

        public async Task<int> RevokeRefreshTokenFamilyAsync(string refreshToken, string? revokedByIp = null)
        {
            var storedRefreshToken = await GetRefreshTokenAsync(refreshToken);

            if (storedRefreshToken is null || string.IsNullOrWhiteSpace(storedRefreshToken.ReplacedByTokenHash))
            {
                return 0;
            }

            var revokedCount = 0;
            var nextTokenHash = storedRefreshToken.ReplacedByTokenHash;

            while (!string.IsNullOrWhiteSpace(nextTokenHash))
            {
                var descendantRefreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(_ => _.TokenHash == nextTokenHash);

                if (descendantRefreshToken is null)
                {
                    break;
                }

                if (descendantRefreshToken.RevokedAtUtc is null)
                {
                    descendantRefreshToken.RevokedAtUtc = DateTime.UtcNow;
                    descendantRefreshToken.RevokedByIp = revokedByIp;
                    revokedCount++;
                }

                nextTokenHash = descendantRefreshToken.ReplacedByTokenHash;
            }

            return revokedCount > 0 ? await _context.SaveChangesAsync() : 0;
        }

        public string GenerateAccessToken(List<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JWT:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddHours(2);
            var token = new JwtSecurityToken(
                _config["JWT:Issuer"],
                _config["JWT:Audience"],
                claims,
                expires: expiration,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private int GetRefreshTokenLifetimeDays()
        {
            return int.TryParse(_config["Runtime:Security:RefreshTokenLifetimeDays"], out var configuredDays)
                && configuredDays > 0
                    ? configuredDays
                    : RefreshTokenLifetimeDaysDefault;
        }

        private static string HashRefreshToken(string refreshToken)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }
    }
}
