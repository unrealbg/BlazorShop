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
                new Category { Id = featuredCategoryId, Name = "Featured" },
                new Category { Id = otherCategoryId, Name = "Other" });

            var product1 = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Product 1",
                Description = "Desc 1",
                Image = "/img/1.png",
                Price = 10m,
                Quantity = 10,
                CategoryId = featuredCategoryId,
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