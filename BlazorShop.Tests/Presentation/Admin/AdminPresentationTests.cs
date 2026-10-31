namespace BlazorShop.Tests.Presentation.Admin;

using System.Reflection;
using BlazorShop.Web.Components.Shared;
using BlazorShop.Web.Layout;
using BlazorShop.Web.Shared.Helper.Contracts;
using BlazorShop.Web.Shared.Services.Contracts;
using BlazorShop.Web.Shared.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public sealed class AdminPresentationTests
{
    [Fact]
    public async Task Dialog_RenderingNamesTheNativeDialogAndHidesInactiveContent()
    {
        using var services = CreateServices(typeof(AdminDialog));
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var visible = await renderer.RenderComponentAsync<AdminDialog>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(AdminDialog.IsVisible)] = true,
                [nameof(AdminDialog.Header)] = (RenderFragment)(builder => builder.AddContent(0, "Stock editor")),
                [nameof(AdminDialog.Body)] = (RenderFragment)(builder => builder.AddContent(0, "Expected stock retained")),
            }));
            var html = visible.ToHtmlString();
            Assert.Contains("<dialog", html);
            Assert.Contains("aria-labelledby=\"admin-dialog-", html);
            Assert.Contains("Stock editor", html);
            Assert.Contains("Expected stock retained", html);
            Assert.Contains("aria-label=\"Close\"", html);
            Assert.DoesNotContain("tabindex", html);
            var hidden = await renderer.RenderComponentAsync<AdminDialog>(ParameterView.Empty);
            Assert.DoesNotContain("v2-admin-dialog-body", hidden.ToHtmlString());
        });
    }

    [Fact]
    public async Task Dialog_CloseNotifiesVisibilityAndCloseOnce()
    {
        var component = new AdminDialog();
        var visibility = new List<bool>();
        var closeCount = 0;
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(AdminDialog.IsVisible)] = true,
            [nameof(AdminDialog.IsVisibleChanged)] = EventCallback.Factory.Create<bool>(this, value => visibility.Add(value)),
            [nameof(AdminDialog.OnClose)] = EventCallback.Factory.Create(this, () => closeCount++),
        }).SetParameterProperties(component);
        await component.CloseAsync();
        await component.CloseAsync();
        Assert.Equal([false], visibility);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task Navigation_AllExistingDestinationsRemainAndOnlyCurrentRouteIsActive()
    {
        using var services = CreateServices(typeof(AdminNavigation));
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AdminNavigation>();
            var html = component.ToHtmlString();
            foreach (var path in new[] { "/admin", "/admin/products", "/admin/categories", "/admin/inventory", "/admin/orders", "/admin/users", "/admin/seo", "/admin/redirects", "/admin/settings", "/admin/audit" })
                Assert.Contains($"href=\"{path}\"", html);
            Assert.Equal(1, html.Split("aria-current=\"page\"").Length - 1);
            Assert.Contains("href=\"/admin/inventory\"", html);
        });
    }

    [Theory]
    [InlineData("/uploads/synthetic.png", "https://api.example.test/uploads/synthetic.png")]
    [InlineData("https://images.example.test/item.webp", "https://images.example.test/item.webp")]
    [InlineData("/images/products/metro-runner.webp", "/images/products/metro-runner.webp")]
    public async Task ProductImage_UsesExistingApiOriginForRelativeUploadPaths(string source, string expected)
    {
        using var services = CreateServices(typeof(AdminProductImage));
        using var client = new HttpClient { BaseAddress = new Uri("https://api.example.test/api/") };
        Mock.Get(services.GetRequiredService<IHttpClientHelper>()).Setup(helper => helper.GetPublicClient()).Returns(client);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AdminProductImage>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(AdminProductImage.Source)] = source }));
            Assert.Contains($"src=\"{expected}\"", component.ToHtmlString());
        });
    }

    [Fact]
    public async Task DialogFeedback_AnnouncesErrorsInsideModalAndEscapesMessageMarkup()
    {
        using var services = CreateServices(typeof(AdminDialogFeedback));
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AdminDialogFeedback>();
            Mock.Get(services.GetRequiredService<IToastService>()).Raise(service => service.OnShow += null, new ToastEventArgs(ToastLevel.Error, "Stock changed <retry>", "Inventory", ToastIcon.Error, ToastPosition.TopRight, false, 5000));
            var html = component.ToHtmlString();
            Assert.Contains("role=\"alert\"", html);
            Assert.Contains("Stock changed &lt;retry&gt;", html);
            Assert.DoesNotContain("<retry>", html);
        });
    }

    private static ServiceProvider CreateServices(Type component)
    {
        var services = new ServiceCollection().AddLogging();
        foreach (var property in component.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(p => p.GetCustomAttribute<InjectAttribute>() is not null))
        {
            if (property.PropertyType == typeof(NavigationManager)) continue;
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(property.PropertyType))!;
            services.AddSingleton(property.PropertyType, mock.Object);
        }
        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        return services.BuildServiceProvider();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/admin/inventory");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
}
