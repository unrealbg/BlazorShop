namespace BlazorShop.Web.Shared.Helper
{
    using System.Net.Http.Json;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;

    public class ApiCallHelper : IApiCallHelper
    {
        public async Task<HttpResponseMessage> ApiCallTypeCall<TModel>(ApiCall apiCall)
        {
            try
            {
                switch (apiCall.Type)
                {
                    case "post":
                        return await apiCall.Client!.PostAsJsonAsync(apiCall.Route, (TModel)apiCall.Model!);
                    case "update":
                        return await apiCall.Client!.PutAsJsonAsync(apiCall.Route, (TModel)apiCall.Model!);
                    case "delete":
                        return await apiCall.Client!.DeleteAsync($"{apiCall.Route}/{apiCall.Id}");
                    case "get":
                        string idRoute = apiCall.Id != null ? $"/{apiCall.Id}" :null!;
                        return await apiCall.Client!.GetAsync($"{apiCall.Route}{idRoute}");
                    default:
                        throw new Exception("Invalid API call type");
                }
            }
            catch
            {
                return null!;
            }
        }

        public async Task<TResponse> GetServiceResponse<TResponse>(HttpResponseMessage responseMassage)
        {
           var response = await responseMassage.Content.ReadFromJsonAsync<TResponse>()!;
           return response!;
        }

        public ServiceResponse ConnectionError()
        {
            return new ServiceResponse(Message: "Error occurred while connecting to the server");
        }
    }
}
