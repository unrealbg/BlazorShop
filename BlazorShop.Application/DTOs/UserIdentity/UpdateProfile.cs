namespace BlazorShop.Application.DTOs.UserIdentity
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateProfile
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? PhoneNumber { get; set; }
    }
}
