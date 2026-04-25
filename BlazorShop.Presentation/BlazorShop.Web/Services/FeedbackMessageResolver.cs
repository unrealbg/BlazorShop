namespace BlazorShop.Web.Services
{
    using System.Net;

    using BlazorShop.Web.Shared.Models;

    public static class FeedbackMessageResolver
    {
        public static string ResolveQueryFailure<T>(QueryResult<T> result, string? fallbackMessage = null)
        {
            var fallback = string.IsNullOrWhiteSpace(fallbackMessage)
                ? "Something went wrong while loading this content. Try again."
                : fallbackMessage;
            var message = string.IsNullOrWhiteSpace(result.Message) ? fallback : result.Message;

            return result.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session has expired. Sign in again and retry.",
                HttpStatusCode.Forbidden => "You do not have permission to access this content.",
                HttpStatusCode.NotFound => message,
                HttpStatusCode.RequestTimeout or HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout => "The service is temporarily unavailable. Try again in a moment.",
                _ when result.StatusCode.HasValue && (int)result.StatusCode.Value >= 500 => fallback,
                _ => message,
            };
        }

        public static string ResolveMutation(ServiceResponse response, string successFallback = "Saved successfully.", string failureFallback = "Request failed.")
        {
            if (!string.IsNullOrWhiteSpace(response.Message))
            {
                return response.Message;
            }

            return response.Success ? successFallback : failureFallback;
        }

        public static string ResolveMutation<TPayload>(ServiceResponse<TPayload> response, string successFallback = "Saved successfully.", string failureFallback = "Request failed.")
        {
            if (!string.IsNullOrWhiteSpace(response.Message))
            {
                return response.Message;
            }

            if (response.Success)
            {
                return successFallback;
            }

            return response.ResponseType switch
            {
                ServiceResponseType.ValidationError => "Please review the highlighted values and try again.",
                ServiceResponseType.NotFound => "We couldn't find the requested content.",
                ServiceResponseType.Conflict => "This data changed while you were editing it. Refresh and try again.",
                _ => failureFallback,
            };
        }
    }
}