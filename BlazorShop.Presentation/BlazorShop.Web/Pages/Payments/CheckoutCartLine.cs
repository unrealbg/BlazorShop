namespace BlazorShop.Web.Pages.Payments
{
    public sealed record CheckoutCartLine(
        Guid ProductId,
        Guid? VariantId,
        string DisplayName,
        string? SizeValue,
        string? Sku,
        string? Color,
        string? ImageUrl,
        decimal UnitPrice,
        int Quantity,
        bool IsUnavailable)
    {
        public decimal LineTotal => UnitPrice * Quantity;

        public string LineTotalDisplay
        {
            get
            {
                try
                {
                    return $"€ {LineTotal:F2}";
                }
                catch (OverflowException)
                {
                    return "Unavailable";
                }
            }
        }

        public string? VariantLabel
        {
            get
            {
                var details = new[]
                    {
                        string.IsNullOrWhiteSpace(Sku) ? null : $"SKU {Sku}",
                        string.IsNullOrWhiteSpace(SizeValue) ? null : $"Size {SizeValue}",
                        string.IsNullOrWhiteSpace(Color) ? null : Color,
                    }
                    .Where(value => value is not null)
                    .ToArray();

                return details.Length == 0 ? null : string.Join(" · ", details);
            }
        }
    }
}
