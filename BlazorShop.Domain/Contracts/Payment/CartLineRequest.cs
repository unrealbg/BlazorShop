namespace BlazorShop.Domain.Contracts.Payment
{
    public sealed record CartLineRequest(
        Guid ProductId,
        Guid? VariantId,
        int Quantity);
}
