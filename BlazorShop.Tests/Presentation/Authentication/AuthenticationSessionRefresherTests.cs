namespace BlazorShop.Tests.Presentation.Authentication;

using System.Net;
using BlazorShop.Web.Authentication.Providers;
using BlazorShop.Web.Shared;
using BlazorShop.Web.Shared.Helper.Contracts;
using BlazorShop.Web.Shared.Models;
using BlazorShop.Web.Shared.Services.Contracts;
using Moq;
using Xunit;

public sealed class AuthenticationSessionRefresherTests
{
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
        services.Tokens.Verify(service => service.StoreJwtTokenAsync(Constant.TokenStorage.Key, "synthetic-access"), Times.Once);
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
        services.Publisher.Verify(service => service.PublishSignedOutAsync(), Times.Once);
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
        services.Publisher.Verify(service => service.PublishSignedOutAsync(), Times.Never);
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
        public Coordinator Coordinator { get; } = new();
        public AuthenticationSessionRefresher Refresher => refresher ??= new(Tokens.Object, Authentication.Object,
            Mock.Of<IAuthenticationStateNotifier>(), Mock.Of<IAuthenticatedClientStateCleaner>(), Publisher.Object, Coordinator);
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
        public Task MarkCurrentEpochAsync() => Task.CompletedTask;
    }
}
