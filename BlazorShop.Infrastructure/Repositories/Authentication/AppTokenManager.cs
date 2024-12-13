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

        public List<Claim> GetUserClaimsFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            return jwtToken is not null ? jwtToken.Claims.ToList() : [];
        }

        public async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
        {
            var user = await _context.RefreshTokens.FirstOrDefaultAsync(_ => _.Token == refreshToken);

            return user is not null;
        }

        public async Task<string> GetUserIdByRefreshTokenAsync(string refreshToken)
        {
            var result = await _context.RefreshTokens.FirstOrDefaultAsync(_ => _.Token == refreshToken);

            return result!.UserId!;
        }

        public async Task<int> AddRefreshTokenAsync(string userId, string refreshToken)
        {
            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                Token = refreshToken,
            });

            return await _context.SaveChangesAsync();
        }

        public async Task<int> UpdateRefreshTokenAsync(string userId, string refreshToken)
        {
            var user = await _context.RefreshTokens.FirstOrDefaultAsync(_ => _.Token == refreshToken);

            if (user is null)
            {
                return -1;
            }

            user.Token = refreshToken;

            return await _context.SaveChangesAsync();
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
    }
}
