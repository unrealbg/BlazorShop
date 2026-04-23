namespace BlazorShop.Web.Pages.Payments
{
    public sealed record CheckoutCartLine(
        Guid ProductId,
        Guid? VariantId,
        string DisplayName,
        string? SizeValue,
        string? ImageUrl,
        decimal UnitPrice,
        int Quantity,
        bool IsUnavailable)
    {
        public decimal LineTotal => UnitPrice * Quantity;

        public string? VariantLabel => string.IsNullOrWhiteSpace(SizeValue)
            ? null
            : $"Size {SizeValue}";
    }
}