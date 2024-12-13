namespace BlazorShop.Domain.Entities.Identity
{
    using Microsoft.AspNetCore.Identity;

    public class AppUser : IdentityUser
    {
        public string FullName { get; set; }
    }
}
