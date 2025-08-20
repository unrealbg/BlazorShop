namespace BlazorShop.Web.Shared.Models
{
    using System.Text.Json;

    public record ServiceResponse(bool Success = false, string Message = null!, Guid? Id = null)
    {
        public JsonElement? Payload { get; init; }
    }
}
