namespace BlazorShop.API
{
    using BlazorShop.Domain.Entities.Identity;

    using Microsoft.AspNetCore.Identity;

    internal static class AdminBootstrapper
    {
        private const string AdminRoleName = "Admin";
        private const string ConfigurationSectionName = "AdminBootstrap";

        public static async Task BootstrapAsync(
            IServiceProvider services,
            IConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(AdminRoleName))
            {
                throw new InvalidOperationException("The Admin role is unavailable. Apply database migrations before bootstrapping an administrator.");
            }

            var existingAdmins = await userManager.GetUsersInRoleAsync(AdminRoleName);
            if (existingAdmins.Count > 0)
            {
                throw new InvalidOperationException("Administrator bootstrap refused because an administrator already exists.");
            }

            var section = configuration.GetSection(ConfigurationSectionName);
            var email = section["Email"]?.Trim();
            var password = section["Password"];
            var fullName = section["FullName"]?.Trim();

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(fullName))
            {
                throw new InvalidOperationException(
                    $"{ConfigurationSectionName}:Email, {ConfigurationSectionName}:Password, and {ConfigurationSectionName}:FullName are required.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                throw new InvalidOperationException("Administrator bootstrap refused because the configured email already belongs to an existing account.");
            }

            var admin = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Administrator creation failed: {FormatErrors(createResult)}");
            }

            var roleResult = await userManager.AddToRoleAsync(admin, AdminRoleName);
            if (roleResult.Succeeded)
            {
                return;
            }

            var deleteResult = await userManager.DeleteAsync(admin);
            var rollbackMessage = deleteResult.Succeeded
                ? string.Empty
                : $" Rollback also failed: {FormatErrors(deleteResult)}";

            throw new InvalidOperationException(
                $"Administrator role assignment failed: {FormatErrors(roleResult)}{rollbackMessage}");
        }

        private static string FormatErrors(IdentityResult result)
        {
            return string.Join(" ", result.Errors.Select(error => error.Description));
        }
    }
}
