namespace BlazorShop.Application.Services.Authentication
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.Services.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Authentication;

    public sealed class SessionObservationService(IAppTokenManager tokens, IAppUserManager users, IAppRoleManager roles)
        : ISessionObservationService
    {
        public async Task<SessionObservation> ObserveAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return SessionObservation.Anonymous;
            var token = await tokens.GetRefreshTokenAsync(refreshToken);
            if (token is null || !tokens.IsRefreshTokenActive(token)) return SessionObservation.Anonymous;
            var user = await users.GetUserByIdAsync(token.UserId);
            if (user is null || string.IsNullOrWhiteSpace(user.Email) || !await users.CanRefreshSignInAsync(user))
                return SessionObservation.Anonymous;
            var role = await roles.GetUserRoleAsync(user.Email);
            if (role is not ("User" or "Admin")) return SessionObservation.Anonymous;
            return new SessionObservation(true, role == "Admin", user.FullName, user.Email);
        }
    }
}
