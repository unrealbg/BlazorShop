namespace BlazorShop.Domain.Contracts.CategoryPersistence
{
    using BlazorShop.Domain.Entities;

    public interface ICategoryRepository
    {
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(Guid categoryId);
    }
}
