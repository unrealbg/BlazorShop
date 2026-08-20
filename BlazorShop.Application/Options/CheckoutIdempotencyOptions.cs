namespace BlazorShop.Application.Options
{
    public sealed class CheckoutIdempotencyOptions
    {
        public const string SectionName = "CheckoutIdempotency";

        public int RetentionDays { get; set; } = 14;

        public int LeaseSeconds { get; set; } = 120;

        public int DuplicateWaitMilliseconds { get; set; } = 2000;

        public int PollMilliseconds { get; set; } = 50;

        public int ProviderRecoveryWindowHours { get; set; } = 23;
    }
}
