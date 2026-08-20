namespace BlazorShop.Domain.Entities.Payment
{
    public sealed class CheckoutIdempotencyRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty;

        public Guid IdempotencyKey { get; set; }

        public string RequestFingerprint { get; set; } = string.Empty;

        public Guid PaymentMethodId { get; set; }

        public CheckoutIdempotencyState State { get; set; } = CheckoutIdempotencyState.Processing;

        public Guid OrderId { get; set; }

        public string OrderReference { get; set; } = string.Empty;

        public string? OutcomeJson { get; set; }

        public int OutcomeVersion { get; set; } = 1;

        public Guid? LeaseOwnerId { get; set; }

        public DateTime? LeaseExpiresOn { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedOn { get; set; }

        public DateTime? ExpiresOn { get; set; }
    }
}
