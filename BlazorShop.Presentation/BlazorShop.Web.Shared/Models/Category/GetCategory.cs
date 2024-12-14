namespace BlazorShop.Web.Shared.Models.Category
{
    public class GetCategory : CategoryBase
    {
        public Guid Id { get; set; }
        
        public ICollection<GetProduct>? Products { get; set; }
    }
}
