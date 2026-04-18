namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class GetSeoRedirect
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? OldPath { get; set; }

        [Required]
        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? NewPath { get; set; }

        public int StatusCode { get; set; } = SeoValidationConstraints.PermanentRedirectStatusCode;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; }
    }
}