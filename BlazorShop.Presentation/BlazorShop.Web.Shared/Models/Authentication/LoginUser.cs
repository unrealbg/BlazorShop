namespace BlazorShop.Web.Shared.Models.Authentication
{
    using System.ComponentModel.DataAnnotations;

    public class LoginUser : AuthenticationBase
    {
        [Required(ErrorMessage = "Password is required.")]
        public string? Password { get; set; }
    }
}
