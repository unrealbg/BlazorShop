namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class UpsertSeoRedirect
    {
        [Required]
        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? OldPath { get; set; }

        [Required]
        [MaxLength(SeoValidationConstraints.UrlMaxLength)]
        public string? NewPath { get; set; }

        [Range(SeoValidationConstraints.PermanentRedirectStatusCode, SeoValidationConstraints.TemporaryRedirectStatusCode)]
        public int StatusCode { get; set; } = SeoValidationConstraints.PermanentRedirectStatusCode;

        public bool IsActive { get; set; } = true;
    }
}