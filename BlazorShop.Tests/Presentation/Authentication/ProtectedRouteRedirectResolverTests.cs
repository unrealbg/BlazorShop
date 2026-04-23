namespace BlazorShop.Tests.Presentation.Authentication
{
    using BlazorShop.Web.Authentication;

    using Xunit;

    public class ProtectedRouteRedirectResolverTests
    {
        [Fact]
        public void ResolveLoginRedirectPath_WhenSignedOut_PreservesProtectedPath()
        {
            var result = ProtectedRouteRedirectResolver.ResolveLoginRedirectPath("account/checkout", isAuthenticated: false);

            Assert.Equal("/authentication/login/account/checkout", result);
        }

        [Fact]
        public void ResolveLoginRedirectPath_WhenSignedOut_StripsQueryAndFragment()
        {
            var result = ProtectedRouteRedirectResolver.ResolveLoginRedirectPath("admin/orders?status=pending#top", isAuthenticated: false);

            Assert.Equal("/authentication/login/admin/orders", result);
        }

        [Fact]
        public void ResolveLoginRedirectPath_WhenAlreadyAuthenticated_ReturnsNull()
        {
            var result = ProtectedRouteRedirectResolver.ResolveLoginRedirectPath("admin", isAuthenticated: true);

            Assert.Null(result);
        }

        [Fact]
        public void ResolvePostLoginPath_WhenRouteIsMissing_ReturnsRoleDefault()
        {
            var customerPath = ProtectedRouteRedirectResolver.ResolvePostLoginPath(null, isAdmin: false);
            var adminPath = ProtectedRouteRedirectResolver.ResolvePostLoginPath(null, isAdmin: true);

            Assert.Equal("/account", customerPath);
            Assert.Equal("/admin", adminPath);
        }

        [Fact]
        public void ResolvePostLoginPath_WhenCustomerRequestedAdminRoute_FallsBackToAccount()
        {
            var result = ProtectedRouteRedirectResolver.ResolvePostLoginPath("admin/orders", isAdmin: false);

            Assert.Equal("/account", result);
        }

        [Fact]
        public void ResolvePostLoginPath_WhenAdminRequestedAdminRoute_PreservesRequestedPath()
        {
            var result = ProtectedRouteRedirectResolver.ResolvePostLoginPath("admin/orders", isAdmin: true);

            Assert.Equal("/admin/orders", result);
        }

        [Fact]
        public void ResolvePostLoginPath_WhenCartRouteRequested_UsesRoleDefault()
        {
            var result = ProtectedRouteRedirectResolver.ResolvePostLoginPath("my-cart", isAdmin: false);

            Assert.Equal("/account", result);
        }
    }
}