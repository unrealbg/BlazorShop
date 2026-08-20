namespace BlazorShop.Application.DTOs.Payment
{
    public sealed record PaymentInitializationResult(
        bool Success,
        string? RedirectUrl = null,
        string? ErrorMessage = null);
}
