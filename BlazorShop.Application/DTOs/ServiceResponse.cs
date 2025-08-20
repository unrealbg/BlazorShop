namespace BlazorShop.Application.DTOs
{
    public record ServiceResponse(bool Success = false, string? Message = null, Guid? Id = null)
    {
        public object? Payload { get; init; }
    }
}
