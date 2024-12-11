namespace BlazorShop.Application.DTOs.Product
{
    using System.ComponentModel.DataAnnotations;

    public class UpdateProduct : ProductBase
    {
        [Required]
        public Guid Id { get; set; }
    }
}
