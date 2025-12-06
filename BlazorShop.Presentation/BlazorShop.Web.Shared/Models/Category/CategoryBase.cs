namespace BlazorShop.Web.Shared.Models.Category
{
    using System.ComponentModel.DataAnnotations;

    public class CategoryBase
    {
        [Required(ErrorMessage = "The Category Name field is required.")]
        public string Name { get; set; } = string.Empty;
    }
}
