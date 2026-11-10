namespace BlazorShop.Infrastructure.Repositories.Authentication
{
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;

    using Microsoft.AspNetCore.Identity;

    public class AppRoleManager : IAppRoleManager
    {
        private readonly UserManager<AppUser> _userManager;

        public AppRoleManager(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public Task<IList<string>> GetUserRolesAsync(AppUser user)
        {
            return _userManager.GetRolesAsync(user);
        }

        public async Task<bool> AddUserToRoleAsync(AppUser user, string roleName)
        {
            var result = await _userManager.AddToRoleAsync(user, roleName);
            return result.Succeeded;
        }
    }
}
