namespace BlazorShop.Web.Shared.Models.Discovery
{
    public sealed class GetProductSitemapEntry
    {
        public string Slug { get; set; } = string.Empty;

        public DateTime? LastModifiedUtc { get; set; }
    }
}