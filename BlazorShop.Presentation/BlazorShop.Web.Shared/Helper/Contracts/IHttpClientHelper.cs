namespace BlazorShop.Web.Shared.Helper.Contracts
{
    public interface IHttpClientHelper
    {
        HttpClient GetPublicClient();

        Task<HttpClient> GetPrivateClientAsync();
    }
}
