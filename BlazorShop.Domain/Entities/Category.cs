namespace BlazorShop.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;

    public class Category
    {
        [Key]
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public ICollection<Product>? Products { get; set; }
    }
}
