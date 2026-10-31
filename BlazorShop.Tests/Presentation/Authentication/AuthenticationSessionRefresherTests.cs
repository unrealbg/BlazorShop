namespace BlazorShop.Tests.Presentation.Authentication;

using System.Net;
using BlazorShop.Web.Authentication.Providers;
using BlazorShop.Web.Shared;
using BlazorShop.Web.Shared.Helper.Contracts;
using BlazorShop.Web.Shared.Models;
using BlazorShop.Web.Shared.Services.Contracts;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

public sealed class AuthenticationSessionRefresherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshNotifiesOnlyAnActualAuthenticationChange(bool changedIdentity)
    {
        var services = new Services();
        var handler = new JwtSecurityTokenHandler();
        var previous = handler.WriteToken(new JwtSecurityToken(claims: [new("name", "Original")], expires: DateTime.UtcNow.AddMinutes(5)));
        var current = handler.WriteToken(new JwtSecurityToken(claims: [new("name", changedIdentity ? "Updated" : "Original")], expires: DateTime.UtcNow.AddHours(2)));
        services.Tokens.Setup(service => service.GetJwtTokenAsync(Constant.TokenStorage.Key)).ReturnsAsync(previous);
        services.Authentication.Setup(service => service.ReviveToken())
            .ReturnsAsync(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "ok", current)));
        Assert.NotNull(await services.Refresher.TryRefreshAsync());
        services.Notifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Exactly(changedIdentity ? 1 : 0));
        Assert.Single(services.Coordinator.CommittedTokens);
    }

    [Fact]
    public async Task BootstrapAndFactoryHandlerScope_ShareTheSameFlight()
    {
        var pending = new TaskCompletionSource<QueryResult<LoginResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new Services();
        services.Authentication.Setup(service => service.ReviveToken()).Returns(pending.Task);
        var registrations = new ServiceCollection();
        registrations.AddSingleton(Mock.Of<Microsoft.JSInterop.IJSRuntime>());
        registrations.AddBrowserSessionCoordination();
        registrations.AddSingleton<IAuthenticationSessionCoordinator>(services.Coordinator);
        registrations.AddSingleton(services.Tokens.Object);
        registrations.AddSingleton(services.Authentication.Object);
        registrations.AddSingleton(Mock.Of<IAuthenticationStateNotifier>());
        registrations.AddSingleton(Mock.Of<IAuthenticatedClientStateCleaner>());
        registrations.AddSingleton(services.Publisher.Object);
        registrations.AddTransient<RefreshTokenHandler>();
        registrations.AddHttpClient("private").AddHttpMessageHandler<RefreshTokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new UnauthorizedThenOk());
        await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var root = provider.GetRequiredService<IAuthenticationSessionRefresher>();
        using var explicitScope = provider.CreateScope();
        Assert.Same(root, explicitScope.ServiceProvider.GetRequiredService<IAuthenticationSessionRefresher>());
        var bootstrap = root.TryRefreshAsync(onlyIfNeeded: true);
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("private");
        var privateRequest = client.GetAsync("https://example.test/orders");
        pending.SetResult(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "ok", "synthetic-access")));
        Assert.NotNull(await bootstrap);
        using var response = await privateRequest;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        services.Authentication.Verify(service => service.ReviveToken(), Times.Once);
    }

    [Fact]
    public async Task Late401WithIdenticalJwtSerialization_DoesNotRotateAgain()
    {
        var services = new Services();
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1)));
        services.Tokens.Setup(service => service.GetJwtTokenAsync(Constant.TokenStorage.Key)).ReturnsAsync(jwt);
        services.Authentication.Setup(service => service.ReviveToken())
            .ReturnsAsync(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "ok", jwt)));
        Assert.NotNull(await services.Refresher.TryRefreshAsync(rejectedToken: jwt));
        Assert.NotNull(await services.Refresher.TryRefreshAsync(rejectedToken: jwt));
        services.Authentication.Verify(service => service.ReviveToken(), Times.Once);
    }

    [Fact]
    public async Task ConcurrentCallers_ShareOneInFlightHttpResult()
    {
        var pending = new TaskCompletionSource<QueryResult<LoginResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new Services();
        services.Authentication.Setup(service => service.ReviveToken()).Returns(pending.Task);
        var first = services.Refresher.TryRefreshAsync();
        var second = services.Refresher.TryRefreshAsync();
        Assert.Same(first, second);
        pending.SetResult(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "ok", "synthetic-access")));
        Assert.NotNull(await first);
        Assert.NotNull(await second);
        services.Authentication.Verify(service => service.ReviveToken(), Times.Once);
        Assert.Single(services.Coordinator.CommittedTokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task FailedOrUncertainResponse_ClearsStateWithoutRetry(HttpStatusCode status)
    {
        var services = new Services();
        services.Authentication.Setup(service => service.ReviveToken()).ReturnsAsync(QueryResult<LoginResponse>.Failed("failed", status));
        Assert.Null(await services.Refresher.TryRefreshAsync());
        services.Authentication.Verify(service => service.ReviveToken(), Times.Once);
        services.Tokens.Verify(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key), Times.Once);
        Assert.Equal(1, services.Coordinator.SignOuts);
    }

    [Fact]
    public async Task LogoutIntentDuringRefresh_DiscardsLateJwt()
    {
        var pending = new TaskCompletionSource<QueryResult<LoginResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new Services();
        services.Authentication.Setup(service => service.ReviveToken()).Returns(pending.Task);
        var refresh = services.Refresher.TryRefreshAsync();
        services.Coordinator.Current = false;
        pending.SetResult(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "ok", "synthetic-access")));
        Assert.Null(await refresh);
        services.Tokens.Verify(service => service.StoreJwtTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(0, services.Coordinator.SignOuts);
    }

    [Fact]
    public async Task LockUnavailable_SendsNoCredentialPost()
    {
        var services = new Services();
        services.Coordinator.Available = false;
        Assert.Null(await services.Refresher.TryRefreshAsync());
        services.Authentication.Verify(service => service.ReviveToken(), Times.Never);
        services.Tokens.Verify(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key), Times.Once);
    }

    private sealed class Services
    {
        public Mock<ITokenService> Tokens { get; } = new();
        public Mock<IAuthenticationService> Authentication { get; } = new();
        public Mock<IAuthenticationSessionEventPublisher> Publisher { get; } = new();
        public Mock<IAuthenticationStateNotifier> Notifier { get; } = new();
        public Coordinator Coordinator { get; } = new();
        public AuthenticationSessionRefresher Refresher => refresher ??= new(Tokens.Object, Authentication.Object,
            Notifier.Object, Mock.Of<IAuthenticatedClientStateCleaner>(), Coordinator);
        private AuthenticationSessionRefresher? refresher;
    }
    private sealed class Coordinator : IAuthenticationSessionCoordinator
    {
        public bool Current { get; set; } = true;
        public bool Available { get; set; } = true;
        public async Task<bool> RunAsync(string operation, Func<Task> action, Func<Task>? onIntent = null)
        { if (Available) await action(); return Available; }
        public Task<bool> IsCurrentAsync() => Task.FromResult(Current);
        public Task<bool> HasCurrentEpochAsync() => Task.FromResult(true);
        public List<string> CommittedTokens { get; } = [];
        public int SignOuts { get; private set; }
        public Task<bool> IsSignedInAsync(string epoch) => Task.FromResult(Current);
        public Task<string> GetEpochAsync() => Task.FromResult("synthetic-epoch");
        public Task<bool> BeginRefreshAsync() => Task.FromResult(Current);
        public Task<bool> CommitTokenAsync(string token) { if (Current) CommittedTokens.Add(token); return Task.FromResult(Current); }
        public Task<bool> InvalidateCurrentAsync() { if (Current) SignOuts++; return Task.FromResult(Current); }
        public Task<bool> PublishSignedInAsync() => Task.FromResult(Current);
    }

    private sealed class UnauthorizedThenOk : HttpMessageHandler
    {
        private int calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(Interlocked.Increment(ref calls) == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
    }
}
