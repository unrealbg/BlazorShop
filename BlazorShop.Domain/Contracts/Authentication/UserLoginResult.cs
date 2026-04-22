namespace BlazorShop.Domain.Contracts.Authentication
{
    public sealed record UserLoginResult(bool Success, bool IsLockedOut = false, bool IsNotAllowed = false);
}