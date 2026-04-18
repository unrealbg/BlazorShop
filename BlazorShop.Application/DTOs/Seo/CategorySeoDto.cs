namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class CategorySeoDto : SeoFieldsDto
    {
        [Required]
        public Guid CategoryId { get; set; }
    }
}