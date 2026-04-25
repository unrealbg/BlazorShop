namespace BlazorShop.Domain.Entities.Identity
{
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public bool RequirePasswordChange { get; set; }
    }
}
