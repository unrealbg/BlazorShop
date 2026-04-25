namespace BlazorShop.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Domain.Constants;

    public class SeoRedirect
    {
        [Key]
        public Guid Id { get; set; }

        public string? OldPath { get; set; }

        public string? NewPath { get; set; }

        public int StatusCode { get; set; } = SeoConstraints.PermanentRedirectStatusCode;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}