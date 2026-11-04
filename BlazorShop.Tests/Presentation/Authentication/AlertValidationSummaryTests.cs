namespace BlazorShop.Tests.Presentation.Authentication;

using System.Reflection;
using BlazorShop.Web.Components.FormControls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public sealed class AlertValidationSummaryTests
{
    [Fact]
    public async Task AnnouncesMessagesOnWrapperWhileKeepingListSemantics()
    {
        using var services = CreateServices();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var model = new LoginModel();
        var editContext = new EditContext(model);
        var messages = new ValidationMessageStore(editContext);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CascadingValue<EditContext>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CascadingValue<EditContext>.Value)] = editContext,
                [nameof(CascadingValue<EditContext>.ChildContent)] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<AlertValidationSummary>(0);
                    builder.AddAttribute(1, "class", "v2-field-full");
                    builder.CloseComponent();
                }),
            }));

            var empty = component.ToHtmlString();
            Assert.DoesNotContain("role=\"alert\"", empty, StringComparison.Ordinal);
            Assert.DoesNotContain("<ul", empty, StringComparison.Ordinal);

            messages.Add(editContext.Field(nameof(LoginModel.Email)), "The Email field is required.");
            editContext.NotifyValidationStateChanged();

            var invalid = component.ToHtmlString();
            Assert.Contains("<div class=\"v2-field-full\" role=\"alert\">", invalid, StringComparison.Ordinal);
            Assert.Contains("<ul class=\"validation-errors\">", invalid, StringComparison.Ordinal);
            Assert.Contains("<li class=\"validation-message\">The Email field is required.</li>", invalid, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(invalid, "role=\""));

            messages.Clear();
            editContext.NotifyValidationStateChanged();

            var cleared = component.ToHtmlString();
            Assert.DoesNotContain("role=\"alert\"", cleared, StringComparison.Ordinal);
            Assert.DoesNotContain("<ul", cleared, StringComparison.Ordinal);
        });
    }

    // The Web _Imports.razor injects shared services into every component; supply inert mocks.
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection().AddLogging();
        var injected = typeof(AlertValidationSummary)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null);
        foreach (var property in injected)
        {
            if (property.PropertyType == typeof(NavigationManager))
            {
                continue;
            }

            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(property.PropertyType))!;
            services.AddSingleton(property.PropertyType, mock.Object);
        }

        services.AddSingleton<NavigationManager>(new TestNavigationManager());
        return services.BuildServiceProvider();
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        for (var index = value.IndexOf(fragment, StringComparison.Ordinal); index >= 0; index = value.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private sealed class LoginModel
    {
        public string? Email { get; set; }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/authentication/login");
    }
}
