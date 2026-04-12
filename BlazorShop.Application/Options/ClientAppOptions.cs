namespace BlazorShop.Application.Options
{
    public class ClientAppOptions
    {
        public const string SectionName = "ClientApp";

        public string BaseUrl { get; set; } = "https://localhost:7258";
    }
}