namespace BlazorShop.Web.Shared.Models.Category
{
    using BlazorShop.Web.Shared.Models.Product;

    public class GetCategory : CategoryBase
    {
        public Guid Id { get; set; }
        
        public ICollection<GetProduct>? Products { get; set; }
    }
}
