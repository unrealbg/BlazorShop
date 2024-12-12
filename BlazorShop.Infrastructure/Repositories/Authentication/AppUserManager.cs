namespace BlazorShop.Infrastructure.Repositories.Authentication
{
    using System.Security.Claims;

    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    public class AppUserManager : IAppUserManager
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IAppRoleManager _roleManager;
        private readonly AppDbContext _context;

        public AppUserManager(IAppRoleManager roleManager, UserManager<AppUser> userManager, AppDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        public async Task<bool> CreateUserAsync(AppUser user)
        {
            var currentUser = await GetUserByEmailAsync(user.Email!);

            if (currentUser != null)
            {
                return false;
            }

            var result = await _userManager.CreateAsync(user!, user!.PasswordHash!);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                return true;
            }

            return false;
        }

        public async Task<bool> LoginUserAsync(AppUser user)
        {
            var currentUser = await GetUserByEmailAsync(user.Email!);

            if (currentUser == null)
            {
                return false;
            }

            string? roleName = await _roleManager.GetUserRoleAsync(user.Email!);

            if (string.IsNullOrEmpty(roleName))
            {
                return false;
            }

            var result = await _userManager.CheckPasswordAsync(currentUser!, user.PasswordHash!);
            return result;
        }

        public async Task<AppUser?> GetUserByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<AppUser?> GetUserByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user!;
        }

        public async Task<IEnumerable<AppUser?>> GetAllUsersAsync()
        {
            //return await this._context.Users.ToListAsync();
        }

        public async Task<int> RemoveUserByEmail(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            _context.Users.Remove(user!);

            return await _context.SaveChangesAsync();
        }

        public async Task<List<Claim>> GetUserClaimsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);

            string? roleName = await _roleManager.GetUserRoleAsync(user!.Email!);

            List<Claim> claims =
                [
                    new Claim("FullName", user!.FullName!),
                    new Claim(ClaimTypes.Email, user!.Email!),
                    new Claim(ClaimTypes.NameIdentifier, user!.Id),
                    new Claim(ClaimTypes.Role, roleName!)
                ];

            return claims;
        }
    }
}
