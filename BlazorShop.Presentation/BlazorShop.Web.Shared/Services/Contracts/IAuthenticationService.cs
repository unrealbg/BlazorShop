namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Authentication;

    public interface IAuthenticationService
    {
        Task<ServiceResponse> CreateUser(CreateUser user);

        Task<LoginResponse> LoginUser(LoginUser user);

        Task<QueryResult<LoginResponse>> ReviveToken();

        Task<ServiceResponse> Logout();

        Task<ServiceResponse> ChangePassword(PasswordChangeModel changePasswordDto);

        Task<ServiceResponse> ConfirmEmail(string userId, string token);

        Task<ServiceResponse> UpdateProfile(UpdateProfileModel model);
    }
}
