namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class GetCategorySeo : SeoFieldsBase
    {
        [Required]
        public Guid CategoryId { get; set; }
    }
}