namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontRedirectQaTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontRedirectQaTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        public static TheoryData<RedirectExpectation> RedirectRoutes =>
        [
            new("/product/legacy-runner", "/product/metro-runner", StorefrontRoutes.Product("metro-runner")),
            new("/category/legacy-sneakers", "/category/sneakers", StorefrontRoutes.Category("sneakers")),
            new("/legacy-sale", "/todays-deals", StorefrontRoutes.TodaysDeals),
        ];

        [Theory]
        [MemberData(nameof(RedirectRoutes))]
        public async Task RedirectSources_ReturnPermanentRedirectsToCanonicalTargets(RedirectExpectation expectation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(expectation.SourcePath);

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal(expectation.ExpectedLocation, response.Headers.Location?.OriginalString);
        }

        [Theory]
        [MemberData(nameof(RedirectRoutes))]
        public async Task RedirectTargets_AreSingleHopPublicPages(RedirectExpectation expectation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var redirectResponse = await client.GetAsync(expectation.SourcePath);
            using var finalResponse = await client.GetAsync(redirectResponse.Headers.Location);
            var finalDocument = await StorefrontHtmlAuditDocument.CreateAsync(finalResponse);

            Assert.Equal(HttpStatusCode.MovedPermanently, redirectResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
            Assert.Single(finalDocument.CanonicalUrls);
            Assert.Equal(StorefrontSeoAuditScenario.AbsoluteUrl(expectation.FinalCanonicalPath), finalDocument.CanonicalUrls[0]);
        }

        [Theory]
        [InlineData("/product/legacy-runner?utm_source=newsletter", "/product/metro-runner")]
        [InlineData("/category/legacy-sneakers?sort=oldest", "/category/sneakers")]
        [InlineData("/legacy-sale?ref=promo", "/todays-deals")]
        public async Task RedirectSources_DropQueryNoiseFromCanonicalTargets(string path, string expectedLocation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task MissingRoutesWithoutRedirects_StayNotFound()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync("/legacy-missing-page");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }

        public sealed record RedirectExpectation(string SourcePath, string ExpectedLocation, string FinalCanonicalPath);
    }
}