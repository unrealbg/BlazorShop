namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Domain.Entities;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Web.Shared.Models.Product;

    using Xunit;

    using static RefreshEligibilityHttpTests;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class PublicCatalogHttpTests(PostgreSqlFixture database) : IAsyncLifetime
    {
        public Task InitializeAsync() => database.ResetDatabaseAsync();

        public Task DisposeAsync() => database.ResetDatabaseAsync();

        [Theory]
        [InlineData("newest")]
        [InlineData("oldest")]
        [InlineData("price-low-to-high")]
        [InlineData("price-high-to-low")]
        [InlineData("name-ascending")]
        [InlineData("name-descending")]
        public async Task PublicCatalog_RealHttpSearchSortCategoryAndPaginationArePublishedOnly(string sort)
        {
            var category = new Category { Id = Guid.NewGuid(), Name = "Published", Slug = "published", IsPublished = true };
            var hiddenCategory = new Category { Id = Guid.NewGuid(), Name = "Hidden", Slug = "hidden", IsPublished = false };
            var otherCategory = new Category { Id = Guid.NewGuid(), Name = "Other", Slug = "other", IsPublished = true };
            var createdOn = DateTime.UtcNow.AddDays(-60);
            var products = Enumerable.Range(1, 30).Select(index => new Product
            {
                Id = Guid.NewGuid(), Name = $"Catalog {index:D2}", Slug = $"catalog-{index}",
                Description = "Blue & red / 雨 + ?#", Price = 31 - index, Quantity = 3,
                CreatedOn = createdOn.AddDays(index), PublishedOn = createdOn, IsPublished = true, CategoryId = category.Id,
            }).ToList();
            await using (var context = database.CreateContext())
            {
                context.Categories.AddRange(category, hiddenCategory, otherCategory);
                context.Products.AddRange(products);
                context.Products.AddRange(
                    new Product { Id = Guid.NewGuid(), Name = "Unpublished", Slug = "unpublished", Description = products[0].Description, Price = 1, IsPublished = false, CategoryId = category.Id },
                    new Product { Id = Guid.NewGuid(), Name = "Hidden parent", Slug = "hidden-parent", Description = products[0].Description, Price = 1, IsPublished = true, CategoryId = hiddenCategory.Id },
                    new Product { Id = Guid.NewGuid(), Name = "Other category", Slug = "other-category", Description = products[0].Description, Price = 1, IsPublished = true, CategoryId = otherCategory.Id });
                await context.SaveChangesAsync();
            }

            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            using var http = new HttpClient(new ApiHandler(host)) { BaseAddress = new Uri("https://localhost/api/") };
            var api = new StorefrontApiClient(http);
            var query = StorefrontCatalogQuery.Parse(products[0].Description, category.Id.ToString(), sort, "1").ToApiQuery();
            var first = await api.GetPublishedCatalogPageAsync(query);
            Assert.True(first.IsSuccess);
            Assert.NotNull(first.Value);
            Assert.Equal(30, first.Value.TotalCount);
            Assert.Equal(24, first.Value.Items.Count);
            var second = await api.GetPublishedCatalogPageAsync(StorefrontCatalogQuery.Parse(products[0].Description, category.Id.ToString(), sort, "2").ToApiQuery());
            Assert.True(second.IsSuccess);
            Assert.NotNull(second.Value);
            Assert.Equal(6, second.Value.Items.Count);
            var expected = query.SortBy switch
            {
                ProductCatalogSortBy.Newest => products.OrderByDescending(product => product.CreatedOn),
                ProductCatalogSortBy.Oldest => products.OrderBy(product => product.CreatedOn),
                ProductCatalogSortBy.PriceLowToHigh => products.OrderBy(product => product.Price),
                ProductCatalogSortBy.PriceHighToLow => products.OrderByDescending(product => product.Price),
                ProductCatalogSortBy.NameAscending => products.OrderBy(product => product.Name),
                _ => products.OrderByDescending(product => product.Name),
            };
            Assert.Equal(expected.Select(product => product.Id), first.Value.Items.Concat(second.Value.Items).Select(product => product.Id));
            var categories = await api.GetPublishedCategoriesAsync();
            Assert.True(categories.IsSuccess);
            Assert.DoesNotContain(categories.Value!, candidate => candidate.Id == hiddenCategory.Id);
            var empty = await api.GetPublishedCatalogPageAsync(new ProductCatalogQuery { SearchTerm = "no match", CategoryId = category.Id });
            Assert.True(empty.IsSuccess);
            Assert.Equal(0, empty.Value!.TotalCount);
            Assert.Empty(empty.Value.Items);
        }

        private sealed class ApiHandler(IdentityHttpHost host) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri);
                return await host.SendRawAsync(forwarded, cancellationToken);
            }
        }
    }
}
