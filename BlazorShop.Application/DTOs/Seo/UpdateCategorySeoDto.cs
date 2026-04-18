namespace BlazorShop.Application.DTOs.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateCategorySeoDto : SeoFieldsDto
    {
        [Required]
        public Guid CategoryId { get; set; }
    }
}