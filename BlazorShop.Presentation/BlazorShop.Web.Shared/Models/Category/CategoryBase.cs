namespace BlazorShop.Web.Shared.Models.Category
{
    using System.ComponentModel.DataAnnotations;

    public class CategoryBase
    {
        [Required]
        public string Name { get; set; }
    }
}
