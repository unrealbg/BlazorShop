namespace BlazorShop.Application.DTOs.Admin.Users
{
    public class AdminUserListItemDto
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
}
