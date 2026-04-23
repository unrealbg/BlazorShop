namespace BlazorShop.Tests.Infrastructure.Repositories
{
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories.CategoryPersistence;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class CategoryRepositoryTests
    {
        [Fact]
        public async Task GetPublishedCategoriesAsync_ReturnsOnlyPublishedCategoriesWithSlugs()
        {
            await using var context = CreateContext();
            context.Categories.AddRange(
                new Category { Id = Guid.NewGuid(), Name = "Featured", Slug = "featured", IsPublished = true },
                new Category { Id = Guid.NewGuid(), Name = "Draft", Slug = "draft", IsPublished = false },
                new Category { Id = Guid.NewGuid(), Name = "Slugless", Slug = null, IsPublished = true });
            await context.SaveChangesAsync();

            var repository = new CategoryRepository(context);

            var result = (await repository.GetPublishedCategoriesAsync()).ToList();

            Assert.Single(result);
            Assert.Equal("Featured", result[0].Name);
            Assert.Equal("featured", result[0].Slug);
        }

        [Fact]
        public async Task GetPublishedCategoryBySlugAsync_ReturnsNullForDraftCategory()
        {
            await using var context = CreateContext();
            context.Categories.AddRange(
                new Category { Id = Guid.NewGuid(), Name = "Featured", Slug = "featured", IsPublished = true },
                new Category { Id = Guid.NewGuid(), Name = "Draft", Slug = "draft", IsPublished = false });
            await context.SaveChangesAsync();

            var repository = new CategoryRepository(context);

            var published = await repository.GetPublishedCategoryBySlugAsync("featured");
            var draft = await repository.GetPublishedCategoryBySlugAsync("draft");

            Assert.NotNull(published);
            Assert.Equal("Featured", published!.Name);
            Assert.Null(draft);
        }

        [Fact]
        public async Task GetPublishedCategoryByIdAsync_ReturnsNullForDraftCategory()
        {
            await using var context = CreateContext();
            var featuredCategory = new Category { Id = Guid.NewGuid(), Name = "Featured", Slug = "featured", IsPublished = true };
            var draftCategory = new Category { Id = Guid.NewGuid(), Name = "Draft", Slug = "draft", IsPublished = false };

            context.Categories.AddRange(featuredCategory, draftCategory);
            await context.SaveChangesAsync();

            var repository = new CategoryRepository(context);

            var published = await repository.GetPublishedCategoryByIdAsync(featuredCategory.Id);
            var draft = await repository.GetPublishedCategoryByIdAsync(draftCategory.Id);

            Assert.NotNull(published);
            Assert.Equal("Featured", published!.Name);
            Assert.Null(draft);
        }

        [Fact]
        public async Task GetPublishedCategorySitemapEntriesAsync_ReturnsPublishedCategorySlugsAndMeaningfulLastModified()
        {
            await using var context = CreateContext();
            var featuredCategoryId = Guid.NewGuid();
            var draftCategoryId = Guid.NewGuid();

            context.Categories.AddRange(
                new Category { Id = featuredCategoryId, Name = "Featured", Slug = "featured", IsPublished = true },
                new Category { Id = draftCategoryId, Name = "Draft", Slug = "draft", IsPublished = false },
                new Category { Id = Guid.NewGuid(), Name = "Slugless", Slug = null, IsPublished = true });

            context.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Published One",
                    Slug = "published-one",
                    CategoryId = featuredCategoryId,
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Published Two",
                    Slug = "published-two",
                    CategoryId = featuredCategoryId,
                    IsPublished = true,
                    PublishedOn = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc),
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Draft Product",
                    Slug = "draft-product",
                    CategoryId = featuredCategoryId,
                    IsPublished = false,
                    PublishedOn = null,
                });

            await context.SaveChangesAsync();

            var repository = new CategoryRepository(context);

            var result = await repository.GetPublishedCategorySitemapEntriesAsync();

            Assert.Single(result);
            Assert.Equal("featured", result[0].Slug);
            Assert.Equal(new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc), result[0].LastModifiedUtc);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"category-repository-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}