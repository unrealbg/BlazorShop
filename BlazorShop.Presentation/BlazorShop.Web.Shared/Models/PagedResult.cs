namespace BlazorShop.Web.Shared.Models
{
    public sealed class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => this.PageSize <= 0
            ? 0
            : (int)Math.Ceiling((double)this.TotalCount / this.PageSize);
    }
}