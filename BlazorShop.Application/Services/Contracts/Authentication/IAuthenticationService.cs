namespace BlazorShop.Application.Services.Contracts.Authentication
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;

    public interface IAuthenticationService
    {
        Task<ServiceResponse> CreateUser(CreateUser user);

        Task<LoginResponse> LoginUser(LoginUser user, string? ipAddress = null, string? userAgent = null);

        Task<LoginResponse> ReviveToken(string refreshToken, string? ipAddress = null, string? userAgent = null);

        Task<ServiceResponse> Logout(string refreshToken, string? ipAddress = null);

        Task<ServiceResponse> ChangePassword(ChangePassword changePasswordDto, string userId);

        Task<ServiceResponse> ConfirmEmail(string email, string token);

        Task<ServiceResponse> UpdateProfile(string userId, UpdateProfile dto);
    }
}
