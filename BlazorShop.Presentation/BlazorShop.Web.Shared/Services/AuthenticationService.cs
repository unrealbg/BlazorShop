namespace BlazorShop.Web.Shared.Services
{
    using System.Web;

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
            return result == null
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
            return result == null
                       ? new LoginResponse(Message: this._apiCallHelper.ConnectionError().Message)
                       : await _apiCallHelper.GetServiceResponse<LoginResponse>(result);
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
            return result == null
                       ? null!
                       : await this._apiCallHelper.GetServiceResponse<LoginResponse>(result);
        }
    }
}
