namespace BlazorShop.Web.Shared.Models.Authentication
{
    using System.ComponentModel.DataAnnotations;

    public class PasswordChangeModel
    {
        [Required]
        public string CurrentPassword { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

}
