namespace BlazorShop.Domain.Contracts
{
    public sealed class CatalogProductReadModel
    {
        public Guid Id { get; init; }

        public string? Slug { get; init; }

        public string? Name { get; init; }

        public string? Description { get; init; }

        public decimal Price { get; init; }

        public string? Image { get; init; }

        public DateTime CreatedOn { get; init; }

        public Guid CategoryId { get; init; }

        public string? CategoryName { get; init; }

        public string? CategorySlug { get; init; }

        public bool HasVariants { get; init; }
    }
}