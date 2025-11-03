namespace BlazorShop.Web.Shared.Models.Product
{
    public class GetProductRecommendation
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        public string? Image { get; set; }

        public decimal Price { get; set; }

        public string? CategoryName { get; set; }
    }
}
