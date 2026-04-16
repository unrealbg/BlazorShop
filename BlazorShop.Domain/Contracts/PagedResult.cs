namespace BlazorShop.Domain.Contracts
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public int PageNumber { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages => this.PageSize <= 0
            ? 0
            : (int)Math.Ceiling((double)this.TotalCount / this.PageSize);
    }
}