namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;

    public interface ISeoRedirectService
    {
        Task<QueryResult<IReadOnlyList<GetSeoRedirect>>> GetAllAsync();

        Task<QueryResult<GetSeoRedirect>> GetByIdAsync(Guid redirectId);

        Task<ServiceResponse<GetSeoRedirect>> CreateAsync(UpsertSeoRedirect request);

        Task<ServiceResponse<GetSeoRedirect>> UpdateAsync(Guid redirectId, UpsertSeoRedirect request);

        Task<ServiceResponse<GetSeoRedirect>> DeactivateAsync(Guid redirectId);

        Task<ServiceResponse<GetSeoRedirect>> DeleteAsync(Guid redirectId);
    }
}