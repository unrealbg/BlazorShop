namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Domain.Constants;

    public class SeoRedirectDto
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? OldPath { get; set; }

        [Required]
        [MaxLength(SeoConstraints.UrlMaxLength)]
        public string? NewPath { get; set; }

        public int StatusCode { get; set; } = SeoConstraints.PermanentRedirectStatusCode;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; }
    }
}