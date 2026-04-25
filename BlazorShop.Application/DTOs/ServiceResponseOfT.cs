namespace BlazorShop.Application.DTOs
{
    public record ServiceResponse<TPayload>(bool Success = false, string? Message = null, Guid? Id = null)
    {
        public TPayload? Payload { get; init; }

        public ServiceResponseType ResponseType { get; init; }
    }
}