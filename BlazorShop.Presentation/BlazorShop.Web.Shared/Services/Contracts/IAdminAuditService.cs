namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Audit;

    public interface IAdminAuditService
    {
        Task<QueryResult<PagedResult<AdminAuditLog>>> GetAsync(AdminAuditQuery query);

        Task<QueryResult<AdminAuditLog>> GetByIdAsync(Guid id);
    }
}
