namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using System.Reflection;

    using BlazorShop.API.Controllers;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    using Xunit;

    public class AuthorizationBoundaryTests
    {
        [Theory]
        [InlineData(typeof(ProductController), nameof(ProductController.GetAll))]
        [InlineData(typeof(CategoryController), nameof(CategoryController.GetAllForAdmin))]
        [InlineData(typeof(ProductVariantController), nameof(ProductVariantController.GetByProductId))]
        [InlineData(typeof(FileUploadController), nameof(FileUploadController.UploadFile))]
        public void SensitiveEndpoints_RequireAdminRole(Type controllerType, string actionName)
        {
            var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(method);

            var authorizeAttribute = method!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
        }

        [Fact]
        public void CategoryAdminRead_UsesExpectedRouteTemplate()
        {
            var method = typeof(CategoryController).GetMethod(nameof(CategoryController.GetAllForAdmin), BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(method);

            var routeAttribute = method!.GetCustomAttribute<HttpGetAttribute>();

            Assert.NotNull(routeAttribute);
            Assert.Equal("all/admin", routeAttribute!.Template);
        }
    }
}