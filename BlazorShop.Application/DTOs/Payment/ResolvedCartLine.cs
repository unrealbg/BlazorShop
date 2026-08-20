namespace BlazorShop.Application.DTOs.Payment
{
    public sealed record ResolvedCartLine(
        Guid ProductId,
        Guid? VariantId,
        int Quantity,
        decimal UnitPrice,
        string ProductName,
        string? ProductDescription,
        string? Sku,
        string? SizeValue,
        string? Color)
    {
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
