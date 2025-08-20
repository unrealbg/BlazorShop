namespace BlazorShop.Web.Shared.Models.Authentication
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateProfileModel
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
