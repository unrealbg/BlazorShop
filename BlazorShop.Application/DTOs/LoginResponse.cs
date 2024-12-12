namespace BlazorShop.Application.DTOs
{
    public record LoginResponse(bool Success = false, string Nessage = null!, string Token = null!, string RefreshToken = null!);
}
