namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;

    using Xunit;

    [Trait("Category", "AuthSmoke")]
    public class AuthSmokeTests
    {
        [AuthSmokeFact]
        public async Task RegisterLoginRefreshAndLogout_RoundTrip_Succeeds()
        {
            using var smokeClient = CreateSmokeClientOrFail();
            var (registration, login) = CreateUniqueUser();

            using var registerResponse = await smokeClient.CreateUserAsync(registration);
            await ReadSuccessfulServiceResponseAsync(registerResponse);

            using var loginResponse = await smokeClient.LoginAsync(login);
            var loginPayload = await ReadSuccessfulLoginResponseAsync(loginResponse);
            Assert.True(string.IsNullOrWhiteSpace(loginPayload.RefreshToken));

            var issuedRefreshCookie = smokeClient.GetRefreshTokenCookieValue();
            Assert.False(string.IsNullOrWhiteSpace(issuedRefreshCookie));

            using var refreshResponse = await smokeClient.RefreshAsync();
            var refreshPayload = await ReadSuccessfulLoginResponseAsync(refreshResponse);
            Assert.True(string.IsNullOrWhiteSpace(refreshPayload.RefreshToken));

            var rotatedRefreshCookie = smokeClient.GetRefreshTokenCookieValue();
            Assert.False(string.IsNullOrWhiteSpace(rotatedRefreshCookie));
            Assert.NotEqual(issuedRefreshCookie, rotatedRefreshCookie);

            using var logoutResponse = await smokeClient.LogoutAsync();
            await ReadSuccessfulServiceResponseAsync(logoutResponse);

            Assert.True(string.IsNullOrWhiteSpace(smokeClient.GetRefreshTokenCookieValue()));
        }

        [AuthSmokeFact]
        public async Task CheckoutHandoff_FollowsAuthenticationState()
        {
            using var smokeClient = CreateSmokeClientOrFail();
            var (registration, login) = CreateUniqueUser();

            using var anonymousCheckoutResponse = await smokeClient.GetCheckoutAsync();
            Assert.Equal(HttpStatusCode.Redirect, anonymousCheckoutResponse.StatusCode);
            Assert.Equal(
                smokeClient.Settings.ResolveClientAppUrl("/authentication/login/account/checkout"),
                anonymousCheckoutResponse.Headers.Location?.ToString());

            using var registerResponse = await smokeClient.CreateUserAsync(registration);
            await ReadSuccessfulServiceResponseAsync(registerResponse);

            using var loginResponse = await smokeClient.LoginAsync(login);
            await ReadSuccessfulLoginResponseAsync(loginResponse);

            using var authenticatedCheckoutResponse = await smokeClient.GetCheckoutAsync();
            Assert.Equal(HttpStatusCode.Redirect, authenticatedCheckoutResponse.StatusCode);
            Assert.Equal(
                smokeClient.Settings.ResolveClientAppUrl("/account/checkout"),
                authenticatedCheckoutResponse.Headers.Location?.ToString());

            using var logoutResponse = await smokeClient.LogoutAsync();
            await ReadSuccessfulServiceResponseAsync(logoutResponse);

            using var loggedOutCheckoutResponse = await smokeClient.GetCheckoutAsync();
            Assert.Equal(HttpStatusCode.Redirect, loggedOutCheckoutResponse.StatusCode);
            Assert.Equal(
                smokeClient.Settings.ResolveClientAppUrl("/authentication/login/account/checkout"),
                loggedOutCheckoutResponse.Headers.Location?.ToString());
        }

        private static AuthSmokeClient CreateSmokeClientOrFail()
        {
            var settings = AuthSmokeSettings.FromEnvironment();
            Assert.True(
                settings.IsEnabled,
                $"Set {AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName}, {AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName}, and {AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName} to run the live auth smoke suite.");

            return new AuthSmokeClient(settings);
        }

        private static (CreateUser Registration, LoginUser Login) CreateUniqueUser()
        {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            var email = $"devsmoke.{suffix}@example.com";
            const string password = "Password123!";

            return (
                new CreateUser
                {
                    Email = email,
                    Password = password,
                    ConfirmPassword = password,
                    FullName = "Dev Smoke User",
                },
                new LoginUser
                {
                    Email = email,
                    Password = password,
                });
        }

        private static async Task<LoginResponse> ReadSuccessfulLoginResponseAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Message);
            Assert.False(string.IsNullOrWhiteSpace(payload.Token));

            return payload;
        }

        private static async Task<ServiceResponse> ReadSuccessfulServiceResponseAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<ServiceResponse>();
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Message);

            return payload;
        }
    }
}