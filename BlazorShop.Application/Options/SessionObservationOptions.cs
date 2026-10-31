namespace BlazorShop.Application.Options
{
    public sealed class SessionObservationOptions
    {
        public const string SectionName = "SessionObservation";
        public const string CredentialHeader = "X-Storefront-Observation-Key";

        public bool Enabled { get; set; }
        public string ServiceKey { get; set; } = string.Empty;
        public bool AllowInsecureDevelopmentTransport { get; set; }

        public bool HasValidKey()
        {
            Span<byte> key = stackalloc byte[32];
            return ServiceKey is { Length: > 0 and <= 64 }
                && Convert.TryFromBase64String(ServiceKey, key, out var written) && written == 32;
        }
    }
}
