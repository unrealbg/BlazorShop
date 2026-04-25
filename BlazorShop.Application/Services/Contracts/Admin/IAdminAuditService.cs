namespace BlazorShop.Application.Services.Contracts.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Domain.Contracts;

    public interface IAdminAuditService
    {
        Task<PagedResult<AdminAuditLogDto>> GetAsync(AdminAuditQueryDto query);

        Task<ServiceResponse<AdminAuditLogDto>> GetByIdAsync(Guid id);

        Task<ServiceResponse<AdminAuditLogDto>> LogAsync(CreateAdminAuditLogDto request);
    }
}
