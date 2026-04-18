namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Web.Shared.Models.Discovery;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Models.Seo;

    using Xunit;

    public class StorefrontApiClientTests
    {
        [Fact]
        public async Task GetSeoSettingsAsync_ReturnsServiceUnavailable_WhenRequestFails()
        {
            using var client = new HttpClient(new ThrowingHttpMessageHandler())
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetSeoSettingsAsync();

            Assert.False(result.IsSuccess);
            Assert.True(result.IsServiceUnavailable);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GetPublishedProductBySlugAsync_ReturnsNotFound_WhenApiReturns404()
        {
            using var client = new HttpClient(new StaticResponseHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)))
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetPublishedProductBySlugAsync("missing-product");

            Assert.False(result.IsSuccess);
            Assert.True(result.IsNotFound);
            Assert.False(result.IsServiceUnavailable);
            Assert.Null(result.Value);
        }

        [Fact]
        public async Task GetPublishedCatalogPageAsync_ReturnsSuccess_WhenApiRespondsWithCatalogPayload()
        {
            var payload = new BlazorShop.Web.Shared.Models.PagedResult<GetCatalogProduct>
            {
                Items = [new GetCatalogProduct { Id = Guid.NewGuid(), Name = "Running Shoes", Slug = "running-shoes", CategoryId = Guid.NewGuid() }],
                PageNumber = 1,
                PageSize = 12,
                TotalCount = 1,
            };

            using var client = new HttpClient(new StaticResponseHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            }))
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetPublishedCatalogPageAsync(new BlazorShop.Web.Shared.Models.Product.ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 12,
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value!.Items);
            Assert.Equal("running-shoes", result.Value.Items[0].Slug);
        }

        [Fact]
        public async Task GetPublishedSitemapAsync_ReturnsSuccess_WhenApiRespondsWithSitemapPayload()
        {
            var payload = new GetPublicCatalogSitemap
            {
                Categories = [new GetCategorySitemapEntry { Slug = "sneakers", LastModifiedUtc = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc) }],
                Products = [new GetProductSitemapEntry { Slug = "metro-runner", LastModifiedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc) }],
            };

            using var client = new HttpClient(new StaticResponseHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            }))
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetPublishedSitemapAsync();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value!.Categories);
            Assert.Single(result.Value.Products);
            Assert.Equal("metro-runner", result.Value.Products[0].Slug);
        }

        [Fact]
        public async Task GetRedirectResolutionAsync_ReturnsNotFound_WhenApiReturns404()
        {
            using var client = new HttpClient(new StaticResponseHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound)))
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetRedirectResolutionAsync("/product/legacy-runner");

            Assert.False(result.IsSuccess);
            Assert.True(result.IsNotFound);
        }

        [Fact]
        public async Task GetRedirectResolutionAsync_ReturnsSuccess_WhenApiRespondsWithRedirectPayload()
        {
            var payload = new SeoRedirectResolutionDto
            {
                NewPath = "/product/metro-runner",
                StatusCode = 301,
            };

            using var client = new HttpClient(new StaticResponseHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            }))
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiClient = new StorefrontApiClient(client);

            var result = await apiClient.GetRedirectResolutionAsync("/product/legacy-runner");

            Assert.True(result.IsSuccess);
            Assert.Equal("/product/metro-runner", result.Value!.NewPath);
            Assert.Equal(301, result.Value.StatusCode);
        }

        private sealed class StaticResponseHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public StaticResponseHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _response.RequestMessage = request;
                return Task.FromResult(_response);
            }
        }

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Connection refused");
            }
        }
    }
}