namespace BlazorShop.Infrastructure.Services.Admin
{
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    public class AdminUserService : IAdminUserService
    {
        private const string AdminRoleName = "Admin";

        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _auditService;

        public AdminUserService(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db,
            IAdminAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _auditService = auditService;
        }

        public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var now = DateTimeOffset.UtcNow;
            var users = _userManager.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var search = query.SearchTerm.Trim().ToLowerInvariant();
                users = users.Where(user =>
                    (user.Email != null && user.Email.ToLower().Contains(search)) ||
                    (user.UserName != null && user.UserName.ToLower().Contains(search)) ||
                    user.FullName.ToLower().Contains(search));
            }

            if (query.Locked.HasValue)
            {
                users = query.Locked.Value
                    ? users.Where(user => user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
                    : users.Where(user => !user.LockoutEnd.HasValue || user.LockoutEnd.Value <= now);
            }

            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                var role = await ResolveRoleNameAsync(query.Role);
                if (role is null)
                {
                    return new PagedResult<AdminUserListItemDto>
                    {
                        Items = Array.Empty<AdminUserListItemDto>(),
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalCount = 0,
                    };
                }

                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                var ids = usersInRole.Select(user => user.Id).ToArray();
                users = users.Where(user => ids.Contains(user.Id));
            }

            var total = await users.CountAsync();
            var page = await users
                .OrderByDescending(user => user.CreatedOn)
                .ThenBy(user => user.Email)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<AdminUserListItemDto>();
            foreach (var user in page)
            {
                items.Add(await MapListItemAsync(user));
            }

            return new PagedResult<AdminUserListItemDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
            };
        }

        public async Task<IReadOnlyList<string>> GetRolesAsync()
        {
            return await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(role => role.Name)
                .Select(role => role.Name!)
                .ToListAsync();
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> GetByIdAsync(string id)
        {
            var user = await FindUserAsync(id);
            return user is null
                ? Failure("User not found.", ServiceResponseType.NotFound)
                : Success(await MapDetailsAsync(user), "User retrieved successfully.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> UpdateRolesAsync(string id, UpdateUserRolesDto request, string? currentAdminUserId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            var desiredRoles = await NormalizeRolesAsync(request.Roles);
            if (!desiredRoles.Success)
            {
                return Failure(desiredRoles.Message, ServiceResponseType.ValidationError);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removingAdmin = currentRoles.Contains(AdminRoleName, StringComparer.OrdinalIgnoreCase) &&
                                !desiredRoles.Roles.Contains(AdminRoleName, StringComparer.OrdinalIgnoreCase);

            if (removingAdmin && string.Equals(user.Id, currentAdminUserId, StringComparison.Ordinal))
            {
                return Failure("You cannot remove your own Admin role.", ServiceResponseType.Conflict);
            }

            if (removingAdmin && await CountAdminsAsync() <= 1)
            {
                return Failure("Cannot remove the last Admin role from the system.", ServiceResponseType.Conflict);
            }

            var rolesToRemove = currentRoles.Except(desiredRoles.Roles, StringComparer.OrdinalIgnoreCase).ToArray();
            var rolesToAdd = desiredRoles.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();

            if (rolesToRemove.Length > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    return Failure(IdentityErrors(removeResult), ServiceResponseType.Failure);
                }
            }

            if (rolesToAdd.Length > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    return Failure(IdentityErrors(addResult), ServiceResponseType.Failure);
                }
            }

            await LogAsync("AdminUser.RolesUpdated", user.Id, $"Roles updated for {DisplayName(user)}.", new { PreviousRoles = currentRoles, Roles = desiredRoles.Roles });
            return Success(await MapDetailsAsync(user), "User roles updated successfully.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> LockAsync(string id, UserLockRequestDto request, string? currentAdminUserId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            if (string.Equals(user.Id, currentAdminUserId, StringComparison.Ordinal))
            {
                return Failure("You cannot lock your own account.", ServiceResponseType.Conflict);
            }

            var lockoutEnd = request.LockoutEnd ?? DateTimeOffset.UtcNow.AddYears(100);
            if (lockoutEnd <= DateTimeOffset.UtcNow)
            {
                return Failure("Lockout end must be in the future.", ServiceResponseType.ValidationError);
            }

            user.LockoutEnabled = true;
            var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
            if (!result.Succeeded)
            {
                return Failure(IdentityErrors(result), ServiceResponseType.Failure);
            }

            await LogAsync("AdminUser.Locked", user.Id, $"User {DisplayName(user)} locked.", new { lockoutEnd, request.Reason });
            return Success(await MapDetailsAsync(user), "User locked successfully.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> UnlockAsync(string id)
        {
            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
            {
                return Failure(IdentityErrors(result), ServiceResponseType.Failure);
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            await LogAsync("AdminUser.Unlocked", user.Id, $"User {DisplayName(user)} unlocked.", null);
            return Success(await MapDetailsAsync(user), "User unlocked successfully.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> ConfirmEmailAsync(string id)
        {
            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return Failure(IdentityErrors(result), ServiceResponseType.Failure);
                }
            }

            await LogAsync("AdminUser.EmailConfirmed", user.Id, $"Email confirmed for {DisplayName(user)}.", null);
            return Success(await MapDetailsAsync(user), "User email confirmed successfully.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> RequirePasswordChangeAsync(string id)
        {
            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            user.RequirePasswordChange = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Failure(IdentityErrors(result), ServiceResponseType.Failure);
            }

            await LogAsync("AdminUser.PasswordChangeRequired", user.Id, $"Password change required for {DisplayName(user)}.", null);
            return Success(await MapDetailsAsync(user), "User will be required to change password.");
        }

        public async Task<ServiceResponse<AdminUserDetailsDto>> DeactivateAsync(string id, string? currentAdminUserId)
        {
            var user = await FindUserAsync(id);
            if (user is null)
            {
                return Failure("User not found.", ServiceResponseType.NotFound);
            }

            if (string.Equals(user.Id, currentAdminUserId, StringComparison.Ordinal))
            {
                return Failure("You cannot deactivate your own account.", ServiceResponseType.Conflict);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains(AdminRoleName, StringComparer.OrdinalIgnoreCase) && await CountAdminsAsync() <= 1)
            {
                return Failure("Cannot deactivate the last Admin account.", ServiceResponseType.Conflict);
            }

            user.LockoutEnabled = true;
            user.RequirePasswordChange = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Failure(IdentityErrors(result), ServiceResponseType.Failure);
            }

            await LogAsync("AdminUser.Deactivated", user.Id, $"User {DisplayName(user)} deactivated.", null);
            return Success(await MapDetailsAsync(user), "User deactivated successfully.");
        }

        private async Task<AppUser?> FindUserAsync(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : await _userManager.FindByIdAsync(id);
        }

        private async Task<int> CountAdminsAsync()
        {
            var admins = await _userManager.GetUsersInRoleAsync(AdminRoleName);
            return admins.Count;
        }

        private async Task<(bool Success, IReadOnlyList<string> Roles, string Message)> NormalizeRolesAsync(IReadOnlyList<string> roles)
        {
            var requestedRoles = roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (requestedRoles.Length == 0)
            {
                return (false, Array.Empty<string>(), "At least one role is required.");
            }

            var availableRoles = await _roleManager.Roles.AsNoTracking().Select(role => role.Name!).ToListAsync();
            var normalizedRoles = new List<string>();
            foreach (var requestedRole in requestedRoles)
            {
                var role = availableRoles.FirstOrDefault(availableRole => string.Equals(availableRole, requestedRole, StringComparison.OrdinalIgnoreCase));
                if (role is null)
                {
                    return (false, Array.Empty<string>(), $"Role '{requestedRole}' does not exist.");
                }

                normalizedRoles.Add(role);
            }

            return (true, normalizedRoles, string.Empty);
        }

        private async Task<string?> ResolveRoleNameAsync(string role)
        {
            var roles = await _roleManager.Roles.AsNoTracking().Select(item => item.Name!).ToListAsync();
            return roles.FirstOrDefault(item => string.Equals(item, role.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private async Task<AdminUserListItemDto> MapListItemAsync(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return new AdminUserListItemDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                Roles = roles.OrderBy(role => role).ToArray(),
                EmailConfirmed = user.EmailConfirmed,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                LockoutEnd = user.LockoutEnd,
                RequirePasswordChange = user.RequirePasswordChange,
                CreatedOn = user.CreatedOn,
            };
        }

        private async Task<AdminUserDetailsDto> MapDetailsAsync(AppUser user)
        {
            var item = await MapListItemAsync(user);
            return new AdminUserDetailsDto
            {
                Id = item.Id,
                Email = item.Email,
                UserName = item.UserName,
                FullName = item.FullName,
                Roles = item.Roles,
                EmailConfirmed = item.EmailConfirmed,
                IsLocked = item.IsLocked,
                LockoutEnd = item.LockoutEnd,
                RequirePasswordChange = item.RequirePasswordChange,
                CreatedOn = item.CreatedOn,
                PhoneNumber = user.PhoneNumber,
                LockoutEnabled = user.LockoutEnabled,
                AccessFailedCount = user.AccessFailedCount,
            };
        }

        private async Task LogAsync(string action, string entityId, string summary, object? metadata)
        {
            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "User",
                EntityId = entityId,
                Summary = summary,
                MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            });
        }

        private static string DisplayName(AppUser user)
        {
            return string.IsNullOrWhiteSpace(user.Email) ? user.Id : user.Email;
        }

        private static string IdentityErrors(IdentityResult result)
        {
            var errors = result.Errors.Select(error => error.Description).Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
            return errors.Length == 0 ? "Identity operation failed." : string.Join(" ", errors);
        }

        private static ServiceResponse<AdminUserDetailsDto> Success(AdminUserDetailsDto payload, string message)
        {
            return new ServiceResponse<AdminUserDetailsDto>(true, message)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<AdminUserDetailsDto> Failure(string message, ServiceResponseType responseType)
        {
            return new ServiceResponse<AdminUserDetailsDto>(false, message)
            {
                ResponseType = responseType,
            };
        }
    }
}
