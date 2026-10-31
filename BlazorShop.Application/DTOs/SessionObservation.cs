namespace BlazorShop.Application.DTOs
{
    public sealed record SessionObservation(bool IsAuthenticated, bool IsAdmin, string? DisplayName, string? Email)
    {
        public static SessionObservation Anonymous { get; } = new(false, false, null, null);
    }
}
