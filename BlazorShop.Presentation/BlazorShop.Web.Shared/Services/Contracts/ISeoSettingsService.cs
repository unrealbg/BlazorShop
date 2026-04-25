namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;

    public interface ISeoSettingsService
    {
        Task<QueryResult<GetSeoSettings>> GetAsync();

        Task<ServiceResponse<GetSeoSettings>> UpdateAsync(UpdateSeoSettings request);
    }
}