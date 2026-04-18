namespace BlazorShop.Domain.Contracts
{
    public sealed class PublishedCategorySitemapEntryReadModel
    {
        public string Slug { get; init; } = string.Empty;

        public DateTime? LastModifiedUtc { get; init; }
    }
}