namespace BlazorShop.Domain.Contracts.CategoryPersistence
{
    using BlazorShop.Domain.Entities;

    public interface ICategoryRepository
    {
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(Guid categoryId);

        Task<IEnumerable<Category>> GetPublishedCategoriesAsync();

        Task<IReadOnlyList<PublishedCategorySitemapEntryReadModel>> GetPublishedCategorySitemapEntriesAsync();

        Task<Category?> GetPublishedCategoryBySlugAsync(string slug);

        Task<bool> CategorySlugExistsAsync(string slug, Guid? excludedCategoryId = null);
    }
}
