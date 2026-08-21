namespace BlazorShop.Domain.Entities.Payment
{
    public sealed class PaymentTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrderId { get; set; }

        public string Provider { get; set; } = string.Empty;

        public string? ProviderSessionId { get; set; }

        public string? ProviderPaymentIntentId { get; set; }

        public long ExpectedAmountMinor { get; set; }

        public string Currency { get; set; } = string.Empty;

        public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? PaidOn { get; set; }

        public DateTime? FailedOn { get; set; }

        public DateTime? CancelledOn { get; set; }

        public Order? Order { get; set; }

        public ICollection<PaymentProviderEvent> ProviderEvents { get; set; } = new List<PaymentProviderEvent>();
    }
}
