namespace BlazorShop.Application.DTOs.Payment
{
    using BlazorShop.Domain.Entities;

    public sealed record ResolvedCartLine(
        Guid ProductId,
        Guid? VariantId,
        int Quantity,
        decimal UnitPrice,
        string ProductName,
        string? ProductDescription,
        string? Sku,
        SizeScale? SizeScale,
        string? SizeValue,
        string? Color)
    {
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
