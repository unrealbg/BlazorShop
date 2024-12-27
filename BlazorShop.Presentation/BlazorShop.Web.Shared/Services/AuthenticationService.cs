namespace BlazorShop.Web.Shared.Services
{
    using System.Net.Http.Json;
    using System.Web;

    using BlazorShop.Web.Shared.Helper;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AuthenticationService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<ServiceResponse> CreateUser(CreateUser user)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Authentication.Register,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Id = null!,
                Model = user,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<CreateUser>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<LoginResponse> LoginUser(LoginUser user)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Authentication.Login,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Id = null!,
                Model = user,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<LoginUser>(currentApiCall);

            if (result is null || !result.IsSuccessStatusCode)
            {
                string errorMessage = "An error occurred.";

                if (result?.Content != null)
                {
                    try
                    {
                        var errorResponse = await result.Content.ReadFromJsonAsync<LoginResponse>();
                        if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                        {
                            errorMessage = errorResponse.Message;
                        }
                    }
                    catch (Exception)
                    {
                        errorMessage = await result.Content.ReadAsStringAsync();
                    }
                }

                return new LoginResponse(Message: errorMessage);
            }

            return await _apiCallHelper.GetServiceResponse<LoginResponse>(result);
        }

        public async Task<LoginResponse> ReviveToken(string refreshToken)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Authentication.ReviveToke,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = HttpUtility.UrlEncode(refreshToken),
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? new LoginResponse(Message: this._apiCallHelper.ConnectionError().Message)
                       : await _apiCallHelper.GetServiceResponse<LoginResponse>(result);

        }

        public async Task<ServiceResponse> ChangePassword(PasswordChangeModel changePasswordDto)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Authentication.ChangePassword,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Id = null!,
                Model = changePasswordDto,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<PasswordChangeModel>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> ConfirmEmail(string userId, string token)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
                                     {
                                         Route = $"{Constant.Authentication.ConfirmEmail}?userId={HttpUtility.UrlEncode(userId)}&token={HttpUtility.UrlEncode(token)}",
                                         Type = Constant.ApiCallType.Get,
                                         Client = client,
                                     };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }


    }
}
