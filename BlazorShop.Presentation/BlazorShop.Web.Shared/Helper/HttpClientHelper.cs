namespace BlazorShop.Web.Shared.Helper
{
    using System.Net.Http.Headers;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;

    public class HttpClientHelper : IHttpClientHelper
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ITokenService _tokenService;

        public HttpClientHelper(IHttpClientFactory clientFactory, ITokenService tokenService)
        {
            _clientFactory = clientFactory;
            _tokenService = tokenService;
        }

        public HttpClient GetPublicClient()
        {
            return _clientFactory.CreateClient(Constant.ApiClient.Name);
        }

        public async Task<HttpClient> GetPrivateClientAsync()
        {
            var client = _clientFactory.CreateClient(Constant.ApiClient.Name);
            string token = await _tokenService.GetJwtTokenAsync(Constant.Cookie.Name);

            if (string.IsNullOrEmpty(token))
            {
                return client;
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
