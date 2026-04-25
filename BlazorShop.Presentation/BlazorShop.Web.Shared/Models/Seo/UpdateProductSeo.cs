namespace BlazorShop.Web.Shared.Models.Seo
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateProductSeo : SeoFieldsBase
    {
        [Required]
        public Guid ProductId { get; set; }

        public DateTime? PublishedOn { get; set; }
    }
}