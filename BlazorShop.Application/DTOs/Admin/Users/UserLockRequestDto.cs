namespace BlazorShop.Application.DTOs.Admin.Users
{
    public class UserLockRequestDto
    {
        public DateTimeOffset? LockoutEnd { get; set; }

        public string? Reason { get; set; }
    }
}
