namespace BlazorShop.Application.DTOs.Admin.Users
{
    public class AdminUserDetailsDto : AdminUserListItemDto
    {
        public string? PhoneNumber { get; set; }

        public bool LockoutEnabled { get; set; }

        public int AccessFailedCount { get; set; }
    }
}
