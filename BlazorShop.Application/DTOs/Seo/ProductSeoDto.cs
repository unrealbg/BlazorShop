namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class ProductSeoDto : SeoFieldsDto
    {
        [Required]
        public Guid ProductId { get; set; }

        public DateTime? PublishedOn { get; set; }
    }
}