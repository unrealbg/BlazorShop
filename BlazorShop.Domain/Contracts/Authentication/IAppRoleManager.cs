namespace BlazorShop.Domain.Contracts.Authentication
{
    using BlazorShop.Domain.Entities.Identity;

    public interface IAppRoleManager
    {
        Task<IList<string>> GetUserRolesAsync(AppUser user);

        Task<bool> AddUserToRoleAsync(AppUser user, string roleName);
    }
}
