namespace BlazorShop.Application.DTOs.Seo
{
    using BlazorShop.Domain.Constants;

    using System.ComponentModel.DataAnnotations;

    public class UpsertSeoRedirectDto
    {
        [Required]
        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? OldPath { get; set; }

        [Required]
        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? NewPath { get; set; }

        public int StatusCode { get; set; } = SeoConstraints.PermanentRedirectStatusCode;

        public bool IsActive { get; set; } = true;
    }
}