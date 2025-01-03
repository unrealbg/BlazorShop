namespace BlazorShop.Application.DTOs.Category
{
    using BlazorShop.Application.DTOs.Product;

    public class GetCategory : CategoryBase
    {
        public Guid Id { get; set; }

        public ICollection<GetProduct>? Products { get; set; }
    }
}