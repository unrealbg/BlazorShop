namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;

    public interface IProductSeoService
    {
        Task<QueryResult<GetProductSeo>> GetByProductIdAsync(Guid productId);

        Task<ServiceResponse<GetProductSeo>> UpdateAsync(Guid productId, UpdateProductSeo request);
    }
}