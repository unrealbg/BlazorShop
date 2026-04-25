namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using System.Reflection;

    using BlazorShop.API.Controllers;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    using Xunit;

    public class AdminSeoAuthorizationTests
    {
        [Theory]
        [InlineData(typeof(AdminProductSeoController), "api/admin/products")]
        [InlineData(typeof(AdminCategorySeoController), "api/admin/categories")]
        [InlineData(typeof(AdminSeoSettingsController), "api/admin/seo/settings")]
        [InlineData(typeof(AdminSeoRedirectsController), "api/admin/seo/redirects")]
        [InlineData(typeof(AdminUsersController), "api/admin/users")]
        [InlineData(typeof(AdminSettingsController), "api/admin/settings")]
        [InlineData(typeof(AdminAuditController), "api/admin/audit")]
        [InlineData(typeof(AdminInventoryController), "api/admin/inventory")]
        [InlineData(typeof(AdminOrdersController), "api/admin/orders")]
        public void Controllers_AreAdminProtectedAndUseExpectedRoute(Type controllerType, string expectedRoute)
        {
            var authorizeAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
            var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
            Assert.NotNull(routeAttribute);
            Assert.Equal(expectedRoute, routeAttribute!.Template);
        }
    }
}
