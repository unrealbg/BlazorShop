namespace BlazorShop.Tests.Presentation.Storefront;

using System.Net;
using System.Text.RegularExpressions;

using BlazorShop.Application.DTOs.Seo;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public sealed class StorefrontInformationPagesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerService_UsesOnlyConfiguredContactsAndEncodesValues(bool configured)
    {
        var settings = new SeoSettingsDto
        {
            BaseCanonicalUrl = StorefrontSeoAuditScenario.BaseUrl,
            CompanyName = configured ? "Synthetic <merchant>" : null,
            CompanyEmail = configured ? "help@example.test" : null,
            CompanyPhone = configured ? "+000 synthetic" : null,
            CompanyAddress = configured ? "Synthetic address <script>" : null,
        };
        using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new() { SeoSettings = settings });
        var html = await client.GetStringAsync("/customer-service");
        Assert.DoesNotContain("support@yourstore.com", html);
        Assert.DoesNotContain("+1 123-456-7890", html);
        Assert.DoesNotContain("123 Main Street", html);
        if (configured)
        {
            Assert.Contains("Synthetic &lt;merchant&gt;", html);
            Assert.Contains("help@example.test", html);
            Assert.Contains("+000 synthetic", WebUtility.HtmlDecode(html));
            Assert.Contains("Synthetic address &lt;script&gt;", html);
            Assert.DoesNotContain("Synthetic address <script>", html);
            Assert.DoesNotContain("Contact details have not been provided", html);
        }
        else Assert.Contains("Contact details have not been provided", html);
    }

    [Fact]
    public async Task Faq_StaticHtmlAndStructuredDataContainExactlyTheSameQuestionsAndAnswers()
    {
        using var client = StorefrontSeoAuditClientFactory.CreateClient(factory);
        var html = await client.GetStringAsync("/faq");
        var document = StorefrontHtmlAuditDocument.Create(html);
        var faq = document.JsonLdBlocks.SelectMany(block => block["@graph"]!.AsArray())
            .Single(node => node?["@type"]?.GetValue<string>() == "FAQPage")!;
        var visible = Regex.Matches(html, @"<details class=""bs-v2-info-panel"">\s*<summary>(.*?)</summary>\s*<p>(.*?)</p>\s*</details>", RegexOptions.Singleline);
        var entities = faq["mainEntity"]!.AsArray();
        Assert.Equal(entities.Count, visible.Count);
        Assert.NotEmpty(entities);
        for (var index = 0; index < entities.Count; index++)
        {
            Assert.Equal(entities[index]!["name"]!.GetValue<string>(), WebUtility.HtmlDecode(visible[index].Groups[1].Value));
            Assert.Equal(entities[index]!["acceptedAnswer"]!["text"]!.GetValue<string>(), WebUtility.HtmlDecode(visible[index].Groups[2].Value));
        }
    }

    [Theory]
    [InlineData("/privacy")]
    [InlineData("/terms")]
    public async Task LegalPages_DiscloseApprovalLimitWithoutInventingPolicies(string path)
    {
        using var client = StorefrontSeoAuditClientFactory.CreateClient(factory);
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("approval before production use", html);
        Assert.Contains("aria-label=\"On this page\"", html);
        Assert.DoesNotContain("30-day", html);
        Assert.DoesNotContain("GDPR compliant", html);
        Assert.Single(StorefrontHtmlAuditDocument.Create(html).CanonicalUrls);
    }
}
