namespace BlazorShop.Tests.Infrastructure.Repositories
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class ProductReadRepositoryTests
    {
        [Fact]
        public async Task GetCatalogPageAsync_ReturnsRequestedPage()
        {
            await using var context = CreateContext();
            var categoryId = Guid.NewGuid();
            await SeedProductsAsync(context, categoryId, Guid.NewGuid());
            var repository = new ProductReadRepository(context);

            var result = await repository.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 2,
                PageSize = 2,
                SortBy = ProductCatalogSortBy.Newest,
            });

            Assert.Equal(2, result.PageNumber);
            Assert.Equal(2, result.PageSize);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal("Product 3", result.Items[0].Name);
            Assert.Equal("Product 2", result.Items[1].Name);
        }

        [Fact]
        public async Task GetCatalogPageAsync_FiltersByCategory()
        {
            await using var context = CreateContext();
            var featuredCategoryId = Guid.NewGuid();
            var otherCategoryId = Guid.NewGuid();
            await SeedProductsAsync(context, featuredCategoryId, otherCategoryId);
            var repository = new ProductReadRepository(context);

            var result = await repository.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 10,
                CategoryId = featuredCategoryId,
            });

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
            Assert.All(result.Items, item => Assert.Equal(featuredCategoryId, item.CategoryId));
        }

        [Fact]
        public async Task GetProductDetailsByIdAsync_ReturnsRequiredCategoryAndVariants()
        {
            await using var context = CreateContext();
            var categoryId = Guid.NewGuid();
            await SeedProductsAsync(context, categoryId, Guid.NewGuid());
            var productId = await context.Products
                .Where(product => product.Name == "Product 5")
                .Select(product => product.Id)
                .SingleAsync();
            var repository = new ProductReadRepository(context);

            var result = await repository.GetProductDetailsByIdAsync(productId);

            Assert.NotNull(result);
            Assert.NotNull(result!.Category);
            Assert.Equal("Featured", result.Category!.Name);
            Assert.Single(result.Variants);
            Assert.Equal("42", result.Variants.Single().SizeValue);
            Assert.Equal(25m, result.Variants.Single().Price);
        }

        [Fact]
        public async Task GetPublishedCatalogPageAsync_ExcludesDraftProductsAndProductsWithoutSlugs()
        {
            await using var context = CreateContext();
            var featuredCategoryId = Guid.NewGuid();
            var otherCategoryId = Guid.NewGuid();
            await SeedProductsAsync(context, featuredCategoryId, otherCategoryId);

            context.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Draft Product",
                    Description = "Draft",
                    Image = "/img/draft.png",
                    Price = 30m,
                    Quantity = 1,
                    CategoryId = featuredCategoryId,
                    Slug = "draft-product",
                    IsPublished = false,
                    PublishedOn = null,
                    CreatedOn = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Slugless Product",
                    Description = "Slugless",
                    Image = "/img/slugless.png",
                    Price = 31m,
                    Quantity = 1,
                    CategoryId = otherCategoryId,
                    Slug = null,
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                    CreatedOn = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                });

            await context.SaveChangesAsync();

            var repository = new ProductReadRepository(context);

            var result = await repository.GetPublishedCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 20,
                SortBy = ProductCatalogSortBy.Newest,
            });

            Assert.Equal(5, result.TotalCount);
            Assert.All(result.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Slug)));
            Assert.DoesNotContain(result.Items, item => item.Name == "Draft Product");
            Assert.DoesNotContain(result.Items, item => item.Name == "Slugless Product");
        }

        [Fact]
        public async Task GetPublishedProductBySlugAsync_ReturnsOnlyPublishedProduct()
        {
            await using var context = CreateContext();
            var featuredCategoryId = Guid.NewGuid();
            await SeedProductsAsync(context, featuredCategoryId, Guid.NewGuid());
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = "Draft Product",
                Description = "Draft",
                Image = "/img/draft.png",
                Price = 35m,
                Quantity = 2,
                CategoryId = featuredCategoryId,
                Slug = "draft-product",
                IsPublished = false,
                PublishedOn = null,
                CreatedOn = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            });
            await context.SaveChangesAsync();

            var repository = new ProductReadRepository(context);

            var publishedResult = await repository.GetPublishedProductBySlugAsync("product-5");
            var draftResult = await repository.GetPublishedProductBySlugAsync("draft-product");

            Assert.NotNull(publishedResult);
            Assert.Equal("Product 5", publishedResult!.Name);
            Assert.NotNull(publishedResult.Category);
            Assert.Single(publishedResult.Variants);
            Assert.Null(draftResult);
        }

        [Fact]
        public async Task GetPublishedProductsByCategoryAsync_ReturnsOnlyPublishedProductsWithSlugs()
        {
            await using var context = CreateContext();
            var featuredCategoryId = Guid.NewGuid();
            await SeedProductsAsync(context, featuredCategoryId, Guid.NewGuid());

            context.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Featured Draft",
                    Description = "Draft",
                    Image = "/img/draft.png",
                    Price = 15m,
                    Quantity = 1,
                    CategoryId = featuredCategoryId,
                    Slug = "featured-draft",
                    IsPublished = false,
                    PublishedOn = null,
                    CreatedOn = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Featured Slugless",
                    Description = "No slug",
                    Image = "/img/no-slug.png",
                    Price = 16m,
                    Quantity = 1,
                    CategoryId = featuredCategoryId,
                    Slug = null,
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
                    CreatedOn = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
                });

            await context.SaveChangesAsync();
            var repository = new ProductReadRepository(context);

            var result = await repository.GetPublishedProductsByCategoryAsync(featuredCategoryId);

            Assert.Equal(3, result.Count);
            Assert.All(result, item => Assert.Equal(featuredCategoryId, item.CategoryId));
            Assert.All(result, item => Assert.False(string.IsNullOrWhiteSpace(item.Slug)));
            Assert.DoesNotContain(result, item => item.Name == "Featured Draft");
            Assert.DoesNotContain(result, item => item.Name == "Featured Slugless");
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"product-read-repository-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }

        private static async Task SeedProductsAsync(AppDbContext context, Guid featuredCategoryId, Guid otherCategoryId)
        {
            context.Categories.AddRange(
                new Category { Id = featuredCategoryId, Name = "Featured", Slug = "featured", IsPublished = true },
                new Category { Id = otherCategoryId, Name = "Other", Slug = "other", IsPublished = true });

            var product1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 1",
                Description = "Desc 1",
                Image = "/img/1.png",
                Price = 10m,
                Quantity = 10,
                CategoryId = featuredCategoryId,
                Slug = "product-1",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                CreatedOn = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
            };

            var product2 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 2",
                Description = "Desc 2",
                Image = "/img/2.png",
                Price = 11m,
                Quantity = 9,
                CategoryId = otherCategoryId,
                Slug = "product-2",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                CreatedOn = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc)
            };

            var product3 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 3",
                Description = "Desc 3",
                Image = "/img/3.png",
                Price = 12m,
                Quantity = 8,
                CategoryId = featuredCategoryId,
                Slug = "product-3",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc),
                CreatedOn = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc)
            };

            var product4 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 4",
                Description = "Desc 4",
                Image = "/img/4.png",
                Price = 13m,
                Quantity = 7,
                CategoryId = otherCategoryId,
                Slug = "product-4",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
                CreatedOn = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
            };

            var product5 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 5",
                Description = "Desc 5",
                Image = "/img/5.png",
                Price = 14m,
                Quantity = 6,
                CategoryId = featuredCategoryId,
                Slug = "product-5",
                IsPublished = true,
                PublishedOn = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc),
                CreatedOn = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc)
            };

            context.Products.AddRange(product1, product2, product3, product4, product5);
            context.ProductVariants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product5.Id,
                SizeScale = SizeScale.ShoesEU,
                SizeValue = "42",
                Price = 25m,
                Stock = 3,
                IsDefault = true,
            });

            await context.SaveChangesAsync();
        }
    }
}