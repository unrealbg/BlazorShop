namespace BlazorShop.Application.Options
{
    public sealed class CheckoutIdempotencyOptions
    {
        public const string SectionName = "CheckoutIdempotency";

        public int RetentionDays { get; set; } = 14;

        public int LeaseSeconds { get; set; } = 120;

        public int DuplicateWaitMilliseconds { get; set; } = 2000;

        public int PollMilliseconds { get; set; } = 50;

        // Bounds safe retries of the original provider Session.Create request. Reaching this
        // deadline does not prove that the payment failed or authorize inventory release.
        public int ProviderRecoveryWindowHours { get; set; } = 23;
    }
}
