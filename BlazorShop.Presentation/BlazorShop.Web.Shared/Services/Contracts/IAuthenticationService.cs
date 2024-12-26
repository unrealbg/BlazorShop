namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Authentication;

    public interface IAuthenticationService
    {
        Task<ServiceResponse> CreateUser(CreateUser user);

        Task<LoginResponse> LoginUser(LoginUser user);

        Task<LoginResponse> ReviveToken(string refreshToken);

        Task<ServiceResponse> ChangePassword(PasswordChangeModel changePasswordDto);
    }
}
