namespace BlazorShop.Web.Shared.Models.Product
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Web.Shared.Models.Category;

    public class GetProduct : ProductBase
    {
        [Required]
        public Guid Id { get; set; }

        public GetCategory? Category { get; set; }

        public DateTime CreatedOn { get; set; }

        public bool IsNew => DateTime.UtcNow.Subtract(this.CreatedOn).TotalDays <= 7;
    }
}
