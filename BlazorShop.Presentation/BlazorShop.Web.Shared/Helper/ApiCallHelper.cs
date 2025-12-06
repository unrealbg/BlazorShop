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
                var client = apiCall.Client ?? throw new ArgumentNullException(nameof(apiCall.Client), "HttpClient cannot be null");
                if (string.IsNullOrWhiteSpace(apiCall.Route))
                {
                    throw new ArgumentException("Route cannot be null or empty", nameof(apiCall.Route));
                }

                var route = apiCall.Route;

                if (string.IsNullOrWhiteSpace(apiCall.Type))
                {
                    throw new ArgumentException("API call type is required.", nameof(apiCall.Type));
                }

                var callType = apiCall.Type.ToLowerInvariant();

                switch (callType)
                {
                    case "post":
                        if (apiCall.Model is HttpContent content)
                        {
                            return await client.PostAsync(route, content);
                        }
                        return await client.PostAsJsonAsync(route, (TModel)apiCall.Model!);
                    case "update":
                        return await client.PutAsJsonAsync(route, (TModel)apiCall.Model!);
                    case "delete":
                        return await client.DeleteAsync($"{route}/{apiCall.Id}");
                    case "get":
                        string idRoute = apiCall.Id != null ? $"/{apiCall.Id}" : string.Empty;
                        return await client.GetAsync($"{route}{idRoute}");
                    default:
                        throw new InvalidOperationException("Invalid API call type");
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new Exception("An error occurred during the HTTP request", httpEx);
            }
            catch (Exception ex)
            {
                throw new Exception("An unexpected error occurred during the API call", ex);
            }
        }

        public async Task<TResponse> GetServiceResponse<TResponse>(HttpResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage), "HttpResponseMessage cannot be null");
            }

            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new Exception($"Request failed with status code: {responseMessage.StatusCode}");
            }

            var response = await responseMessage.Content.ReadFromJsonAsync<TResponse>();
            if (response == null)
            {
                throw new Exception("Failed to deserialize the response");
            }

            return response;
        }

        public ServiceResponse ConnectionError()
        {
            return new ServiceResponse(Message: "Error occurred while connecting to the server");
        }
    }
}
