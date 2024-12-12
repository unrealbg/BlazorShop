namespace BlazorShop.Domain.Contracts.Authentication
{
    using BlazorShop.Domain.Entities.Identity;

    public interface IAppRoleManager
    {
        Task<string?> GetUserRoleAsync(string userEmail);

        Task<bool> AddUserToRoleAsync(AppUser user, string roleName);
    }
}
