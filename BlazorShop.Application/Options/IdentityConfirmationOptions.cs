namespace BlazorShop.Application.Options
{
    public sealed class IdentityConfirmationOptions
    {
        public const string SectionName = "Identity";

        public bool RequireConfirmedAccount { get; set; } = true;

        public bool RequireConfirmedEmail { get; set; } = true;
    }
}