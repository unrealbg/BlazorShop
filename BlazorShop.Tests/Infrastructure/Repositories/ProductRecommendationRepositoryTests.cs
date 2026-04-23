namespace BlazorShop.Tests.Infrastructure.Repositories
{
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class ProductRecommendationRepositoryTests
    {
        [Fact]
        public async Task GetFrequentlyBoughtTogetherAsync_ExcludesNonPublicProducts()
        {
            await using var context = CreateContext();
            var categoryId = Guid.NewGuid();
            var sourceProductId = Guid.NewGuid();
            var publicRelatedProductId = Guid.NewGuid();
            var draftRelatedProductId = Guid.NewGuid();

            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "Featured",
                Slug = "featured",
                IsPublished = true,
            });

            context.Products.AddRange(
                new Product
                {
                    Id = sourceProductId,
                    Name = "Source Product",
                    Description = "Source",
                    Image = "/img/source.png",
                    Price = 20m,
                    Quantity = 5,
                    CategoryId = categoryId,
                    Slug = "source-product",
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                    CreatedOn = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = publicRelatedProductId,
                    Name = "Public Related",
                    Description = "Public",
                    Image = "/img/public.png",
                    Price = 25m,
                    Quantity = 5,
                    CategoryId = categoryId,
                    Slug = "public-related",
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                    CreatedOn = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = draftRelatedProductId,
                    Name = "Draft Related",
                    Description = "Draft",
                    Image = "/img/draft.png",
                    Price = 30m,
                    Quantity = 5,
                    CategoryId = categoryId,
                    Slug = "draft-related",
                    IsPublished = false,
                    PublishedOn = null,
                    CreatedOn = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc),
                });

            var orderId = Guid.NewGuid();
            context.Orders.Add(new Order
            {
                Id = orderId,
                UserId = "user-1",
                Status = "Paid",
                Reference = "order-1",
                TotalAmount = 45m,
                Lines =
                [
                    new OrderLine { OrderId = orderId, ProductId = sourceProductId, Quantity = 1, UnitPrice = 20m },
                    new OrderLine { OrderId = orderId, ProductId = publicRelatedProductId, Quantity = 1, UnitPrice = 25m },
                    new OrderLine { OrderId = orderId, ProductId = draftRelatedProductId, Quantity = 1, UnitPrice = 30m },
                ]
            });

            await context.SaveChangesAsync();

            var logger = new Mock<IAppLogger<ProductRecommendationRepository>>();
            var repository = new ProductRecommendationRepository(context, logger.Object);

            var result = (await repository.GetFrequentlyBoughtTogetherAsync(sourceProductId, 10)).ToList();

            Assert.Single(result);
            Assert.Equal(publicRelatedProductId, result[0].Id);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"product-recommendation-repository-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}