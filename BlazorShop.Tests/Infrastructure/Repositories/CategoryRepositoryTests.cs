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

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"category-repository-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}