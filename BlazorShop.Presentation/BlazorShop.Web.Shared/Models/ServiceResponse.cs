namespace BlazorShop.Web.Shared.Models
{
    using System.Text.Json;

    public record ServiceResponse(bool Success = false, string Message = null!, Guid? Id = null)
    {
        public JsonElement? Payload { get; init; }
    }

    public record ServiceResponse<TPayload>(bool Success = false, string Message = null!, Guid? Id = null)
    {
        public TPayload? Payload { get; init; }

        public ServiceResponseType ResponseType { get; init; }
    }
}
