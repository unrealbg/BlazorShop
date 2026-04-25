namespace BlazorShop.Web.Shared.Helper
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Text.Json;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;

    using Microsoft.Extensions.Logging;

    public class ApiCallHelper : IApiCallHelper
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly ILogger<ApiCallHelper>? _logger;

        public ApiCallHelper()
        {
        }

        public ApiCallHelper(ILogger<ApiCallHelper> logger)
        {
            _logger = logger;
        }

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
                HttpResponseMessage response = callType switch
                {
                    "post" => apiCall.Model is HttpContent content
                        ? await client.PostAsync(route, content)
                        : apiCall.Model is null
                            ? await client.PostAsync(route, content: null)
                        : await client.PostAsJsonAsync(route, (TModel)apiCall.Model!),
                    "update" => await client.PutAsJsonAsync(route, (TModel)apiCall.Model!),
                    "delete" => await client.DeleteAsync($"{route}/{apiCall.Id}"),
                    "get" => await client.GetAsync($"{route}{(apiCall.Id != null ? $"/{apiCall.Id}" : string.Empty)}"),
                    _ => throw new InvalidOperationException("Invalid API call type")
                };

                LogFailureResponse(apiCall, response);
                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogWarning(ex, "API call {Method} {Route} failed while reaching the server.", apiCall.Type, apiCall.Route);
                return CreateServiceUnavailableResponse(apiCall, ConnectionError().Message);
            }
            catch (OperationCanceledException ex)
            {
                _logger?.LogWarning(ex, "API call {Method} {Route} timed out.", apiCall.Type, apiCall.Route);
                return CreateServiceUnavailableResponse(apiCall, "The request timed out while connecting to the server");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during API call {Method} {Route}.", apiCall.Type, apiCall.Route);
                throw new InvalidOperationException(
                    $"An unexpected error occurred during the API call to {apiCall.Client?.BaseAddress}{apiCall.Route}",
                    ex);
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

            var mediaType = responseMessage.Content.Headers.ContentType?.MediaType;
            if (!IsJsonMediaType(mediaType))
            {
                var responseBody = await responseMessage.Content.ReadAsStringAsync();
                var responsePreview = responseBody.Length > 120
                                          ? responseBody[..120]
                                          : responseBody;

                throw new InvalidOperationException(
                    $"Expected JSON from {responseMessage.RequestMessage?.RequestUri}, but received {mediaType ?? "no content type"}. Response starts with: {responsePreview}");
            }

            var response = await TryReadJsonResponse<TResponse>(responseMessage);
            if (response == null)
            {
                throw new Exception("Failed to deserialize the response");
            }

            return response;
        }

        public async Task<ServiceResponse<TPayload>> GetMutationResponse<TPayload>(HttpResponseMessage responseMessage, string defaultErrorMessage)
        {
            if (responseMessage == null)
            {
                return new ServiceResponse<TPayload>(Message: defaultErrorMessage)
                {
                    ResponseType = ServiceResponseType.Failure,
                };
            }

            var response = await TryReadJsonResponse<ServiceResponse<TPayload>>(responseMessage);
            if (response is not null)
            {
                if (responseMessage.IsSuccessStatusCode && response.ResponseType == ServiceResponseType.None)
                {
                    return response with { ResponseType = ServiceResponseType.Success };
                }

                return response;
            }

            if (responseMessage.IsSuccessStatusCode)
            {
                return new ServiceResponse<TPayload>(Success: true, Message: string.Empty)
                {
                    ResponseType = ServiceResponseType.Success,
                };
            }

            var message = ShouldUseDefaultErrorMessage(responseMessage.StatusCode)
                ? defaultErrorMessage
                : await GetFailureMessageAsync(responseMessage, defaultErrorMessage);

            return new ServiceResponse<TPayload>(Message: message)
            {
                ResponseType = MapFailureType(responseMessage.StatusCode),
            };
        }

        public async Task<QueryResult<TResponse>> GetQueryResult<TResponse>(HttpResponseMessage responseMessage, string defaultErrorMessage)
        {
            if (responseMessage == null)
            {
                return QueryResult<TResponse>.Failed(defaultErrorMessage);
            }

            if (responseMessage.IsSuccessStatusCode)
            {
                return QueryResult<TResponse>.Succeeded(await GetServiceResponse<TResponse>(responseMessage));
            }

            var message = ShouldUseDefaultErrorMessage(responseMessage.StatusCode)
                ? defaultErrorMessage
                : await GetFailureMessageAsync(responseMessage, defaultErrorMessage);

            return QueryResult<TResponse>.Failed(message, responseMessage.StatusCode);
        }

        public ServiceResponse ConnectionError()
        {
            return new ServiceResponse(Message: "Error occurred while connecting to the server");
        }

        private static HttpResponseMessage CreateServiceUnavailableResponse(ApiCall apiCall, string message)
        {
            var requestUri = apiCall.Client?.BaseAddress is not null &&
                             Uri.TryCreate(apiCall.Client.BaseAddress, apiCall.Route, out var uri)
                ? uri
                : null;
            var method = apiCall.Type?.ToUpperInvariant() ?? HttpMethod.Get.Method;

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = requestUri is null ? null : new HttpRequestMessage(new HttpMethod(method), requestUri),
                Content = JsonContent.Create(new { message })
            };
        }

        private void LogFailureResponse(ApiCall apiCall, HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            _logger?.LogWarning(
                "API call {Method} {Route} returned status code {StatusCode}.",
                apiCall.Type,
                apiCall.Route,
                (int)response.StatusCode);
        }

        private async Task<string> GetFailureMessageAsync(HttpResponseMessage responseMessage, string defaultErrorMessage)
        {
            if (responseMessage.Content is null)
            {
                return defaultErrorMessage;
            }

            var mediaType = responseMessage.Content.Headers.ContentType?.MediaType;
            if (!IsJsonMediaType(mediaType))
            {
                return defaultErrorMessage;
            }

            try
            {
                using var document = JsonDocument.Parse(await responseMessage.Content.ReadAsStringAsync());
                if (TryGetJsonString(document.RootElement, "message", out var message) &&
                    !string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }

                if (TryGetJsonString(document.RootElement, "detail", out var detail) &&
                    !string.IsNullOrWhiteSpace(detail))
                {
                    return detail;
                }

                if (TryGetJsonString(document.RootElement, "title", out var title) &&
                    !string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to parse error payload for status code {StatusCode}.", (int)responseMessage.StatusCode);
            }

            return defaultErrorMessage;
        }

        private static bool TryGetJsonString(JsonElement element, string propertyName, out string? value)
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString();
                return true;
            }

            value = null;
            return false;
        }

        private static bool ShouldUseDefaultErrorMessage(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500;
        }

        private async Task<TResponse?> TryReadJsonResponse<TResponse>(HttpResponseMessage responseMessage)
        {
            if (responseMessage.Content is null)
            {
                return default;
            }

            var mediaType = responseMessage.Content.Headers.ContentType?.MediaType;
            if (!IsJsonMediaType(mediaType))
            {
                return default;
            }

            var payload = await responseMessage.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<TResponse>(payload, SerializerOptions);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to deserialize JSON response from {RequestUri}.", responseMessage.RequestMessage?.RequestUri);
                return default;
            }
        }

        private static ServiceResponseType MapFailureType(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => ServiceResponseType.ValidationError,
                HttpStatusCode.NotFound => ServiceResponseType.NotFound,
                HttpStatusCode.Conflict => ServiceResponseType.Conflict,
                _ => ServiceResponseType.Failure,
            };
        }

        private static bool IsJsonMediaType(string? mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
