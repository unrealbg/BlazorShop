namespace BlazorShop.Domain.Contracts.Authentication
{
    using System.Security.Claims;

    using BlazorShop.Domain.Entities.Identity;

    public interface IAppUserManager
    {
        Task<bool> CreateUserAsync(AppUser user);

        Task<bool> LoginUserAsync(AppUser user);

        Task<AppUser?> GetUserByEmailAsync(string email);

        Task<AppUser?> GetUserByIdAsync(string id);

        Task<IEnumerable<AppUser?>> GetAllUsersAsync();

        Task<int> RemoveUserByEmail(string email);

        Task<List<Claim>> GetUserClaimsAsync(string email);

        Task<bool> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword);

        Task<bool> CheckPasswordAsync(AppUser user, string password);

        Task<string> GenerateEmailConfirmationTokenAsync(AppUser user);

        Task<bool> ConfirmEmailAsync(AppUser user, string token);

    }
}
