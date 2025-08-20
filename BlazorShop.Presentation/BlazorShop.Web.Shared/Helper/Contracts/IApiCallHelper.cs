namespace BlazorShop.Web.Shared.Helper.Contracts
{
    using System.Net.Http;
    using BlazorShop.Web.Shared.Models;

    public interface IApiCallHelper
    {
        Task<HttpResponseMessage> ApiCallTypeCall<TModel>(ApiCall apiCall);

        Task<TResponse> GetServiceResponse<TResponse>(HttpResponseMessage responseMessage);

        ServiceResponse ConnectionError();
    }
}
