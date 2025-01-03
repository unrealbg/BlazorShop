namespace BlazorShop.Web.Shared.Models.Authentication
{
    using System.ComponentModel.DataAnnotations;

    public class AuthenticationBase
    {
        [EmailAddress, Required]
        public string? Email { get; set; }
    }
}
