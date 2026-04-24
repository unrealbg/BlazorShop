namespace BlazorShop.Web.Shared.Models.Admin.Users
{
    public class AdminUserListItem
    {
        public string Id { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? UserName { get; set; }

        public string FullName { get; set; } = string.Empty;

        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

        public bool EmailConfirmed { get; set; }

        public bool IsLocked { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public bool RequirePasswordChange { get; set; }

        public DateTime CreatedOn { get; set; }
    }

    public class AdminUserDetails : AdminUserListItem
    {
        public string? PhoneNumber { get; set; }

        public bool LockoutEnabled { get; set; }

        public int AccessFailedCount { get; set; }
    }

    public class AdminUserQuery
    {
        public string? SearchTerm { get; set; }

        public string? Role { get; set; }

        public bool? Locked { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }

    public class UpdateUserRoles
    {
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }

    public class UserLockRequest
    {
        public DateTimeOffset? LockoutEnd { get; set; }

        public string? Reason { get; set; }
    }
}
