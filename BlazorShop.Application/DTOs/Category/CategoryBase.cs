namespace BlazorShop.Application.DTOs.Category
{
    using System.ComponentModel.DataAnnotations;

    public class CategoryBase
    {
        [Required]
        public string? Name { get; set; }
    }
}
