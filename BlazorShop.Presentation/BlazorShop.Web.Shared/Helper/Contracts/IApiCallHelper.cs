namespace BlazorShop.Web.Shared.Helper.Contracts
{
    using BlazorShop.Web.Shared.Models;

    public interface IApiCallHelper
    {
        Task<HttpResponseMessage> ApiCallTypeCall<TModel>(ApiCall apiCall);

        Task<TResponse> GetServiceResponse<TResponse>(HttpResponseMessage responseMassage);

        ServiceResponse ConnectionError();
    }
}
