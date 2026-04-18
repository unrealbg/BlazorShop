namespace BlazorShop.Storefront.Services
{
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;

    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Models.Seo;

    public class StorefrontApiClient
    {
        // Static informational pages should degrade faster than catalog-backed pages when the API is offline.
        private static readonly TimeSpan CatalogRequestTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SeoSettingsRequestTimeout = TimeSpan.FromMilliseconds(500);
        private const string PublicCatalogBaseRoute = "public/catalog";
        private const string PublicCategoriesRoute = PublicCatalogBaseRoute + "/categories";
        private const string PublicProductsRoute = PublicCatalogBaseRoute + "/products";
        private const string SeoSettingsRoute = "seo/settings";

        private readonly HttpClient _httpClient;

        public StorefrontApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<StorefrontApiResult<IReadOnlyList<GetCategory>>> GetPublishedCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var result = await GetAsync<List<GetCategory>>(PublicCategoriesRoute, cancellationToken, []);

            return result.IsSuccess && result.Value is not null
                ? StorefrontApiResult<IReadOnlyList<GetCategory>>.Success(result.Value)
                : result.IsServiceUnavailable
                    ? StorefrontApiResult<IReadOnlyList<GetCategory>>.ServiceUnavailable()
                    : StorefrontApiResult<IReadOnlyList<GetCategory>>.Success([]);
        }

        public Task<StorefrontApiResult<PagedResult<GetCatalogProduct>>> GetPublishedCatalogPageAsync(ProductCatalogQuery query, CancellationToken cancellationToken = default)
        {
            return GetAsync(BuildCatalogRoute(query), cancellationToken, new PagedResult<GetCatalogProduct>(), CatalogRequestTimeout);
        }

        public Task<StorefrontApiResult<GetCategoryPage>> GetPublishedCategoryBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return GetMaybeNotFoundAsync<GetCategoryPage>($"{PublicCategoriesRoute}/slug/{Uri.EscapeDataString(slug)}", cancellationToken, CatalogRequestTimeout);
        }

        public Task<StorefrontApiResult<GetProduct>> GetPublishedProductBySlugAsync(string slug, CancellationToken cancellationToken = default)
        {
            return GetMaybeNotFoundAsync<GetProduct>($"{PublicProductsRoute}/slug/{Uri.EscapeDataString(slug)}", cancellationToken, CatalogRequestTimeout);
        }

        public Task<StorefrontApiResult<GetSeoSettings>> GetSeoSettingsAsync(CancellationToken cancellationToken = default)
        {
            return GetAsync<GetSeoSettings>(SeoSettingsRoute, cancellationToken, requestTimeout: SeoSettingsRequestTimeout);
        }

        private async Task<StorefrontApiResult<T>> GetAsync<T>(string route, CancellationToken cancellationToken, T? fallbackValue = default, TimeSpan? requestTimeout = null)
        {
            using var requestTimeoutToken = CreateRequestTimeoutToken(cancellationToken, requestTimeout ?? CatalogRequestTimeout);

            try
            {
                var payload = await _httpClient.GetFromJsonAsync<T>(route, requestTimeoutToken.Token);
                if (payload is not null)
                {
                    return StorefrontApiResult<T>.Success(payload);
                }

                return fallbackValue is not null
                    ? StorefrontApiResult<T>.Success(fallbackValue)
                    : StorefrontApiResult<T>.NotFound();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StorefrontApiResult<T>.ServiceUnavailable();
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
            {
                return StorefrontApiResult<T>.ServiceUnavailable();
            }
        }

        private async Task<StorefrontApiResult<T>> GetMaybeNotFoundAsync<T>(string route, CancellationToken cancellationToken, TimeSpan requestTimeout)
        {
            using var requestTimeoutToken = CreateRequestTimeoutToken(cancellationToken, requestTimeout);

            try
            {
                using var response = await _httpClient.GetAsync(route, requestTimeoutToken.Token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return StorefrontApiResult<T>.NotFound();
                }

                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
                return payload is not null
                    ? StorefrontApiResult<T>.Success(payload)
                    : StorefrontApiResult<T>.NotFound();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StorefrontApiResult<T>.ServiceUnavailable();
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
            {
                return StorefrontApiResult<T>.ServiceUnavailable();
            }
        }

        private static CancellationTokenSource CreateRequestTimeoutToken(CancellationToken cancellationToken, TimeSpan requestTimeout)
        {
            var requestTimeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeoutToken.CancelAfter(requestTimeout);
            return requestTimeoutToken;
        }

        private static string BuildCatalogRoute(ProductCatalogQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Max(1, query.PageSize)}",
                $"sortBy={Uri.EscapeDataString(query.SortBy.ToString())}",
            };

            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
            {
                parameters.Add($"categoryId={query.CategoryId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                parameters.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
            }

            if (query.CreatedAfterUtc.HasValue)
            {
                parameters.Add($"createdAfterUtc={Uri.EscapeDataString(query.CreatedAfterUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
            }

            return $"{PublicProductsRoute}?{string.Join("&", parameters)}";
        }
    }
}