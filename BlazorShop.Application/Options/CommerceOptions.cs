namespace BlazorShop.Application.Options
{
    public sealed class CommerceOptions
    {
        public const string SectionName = "Commerce";

        public string Currency { get; set; } = string.Empty;
    }
}
