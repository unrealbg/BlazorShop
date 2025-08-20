namespace BlazorShop.Application.DTOs.Payment
{
    public class UpdateTrackingRequest
    {
        public string Carrier { get; set; } = string.Empty;

        public string TrackingNumber { get; set; } = string.Empty;

        public string TrackingUrl { get; set; } = string.Empty;
    }
}
