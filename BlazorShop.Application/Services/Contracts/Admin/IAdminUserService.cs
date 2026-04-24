namespace BlazorShop.Application.Services.Contracts.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Domain.Contracts;

    public interface IAdminUserService
    {
        Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query);

        Task<IReadOnlyList<string>> GetRolesAsync();

        Task<ServiceResponse<AdminUserDetailsDto>> GetByIdAsync(string id);

        Task<ServiceResponse<AdminUserDetailsDto>> UpdateRolesAsync(string id, UpdateUserRolesDto request, string? currentAdminUserId);

        Task<ServiceResponse<AdminUserDetailsDto>> LockAsync(string id, UserLockRequestDto request, string? currentAdminUserId);

        Task<ServiceResponse<AdminUserDetailsDto>> UnlockAsync(string id);

        Task<ServiceResponse<AdminUserDetailsDto>> ConfirmEmailAsync(string id);

        Task<ServiceResponse<AdminUserDetailsDto>> RequirePasswordChangeAsync(string id);

        Task<ServiceResponse<AdminUserDetailsDto>> DeactivateAsync(string id, string? currentAdminUserId);
    }
}
