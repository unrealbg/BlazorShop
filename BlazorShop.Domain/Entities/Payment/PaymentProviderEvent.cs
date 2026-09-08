namespace BlazorShop.Domain.Entities.Payment
{
    public sealed class PaymentProviderEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Provider { get; set; } = string.Empty;

        public string ProviderEventId { get; set; } = string.Empty;

        public Guid? PaymentTransactionId { get; set; }

        public Guid? OrderId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public DateTime ProviderCreatedOn { get; set; }

        public PaymentProviderEventOutcome ProcessingOutcome { get; set; } = PaymentProviderEventOutcome.Pending;

        public string? FailureReason { get; set; }

        public DateTime ReceivedOn { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedOn { get; set; }

        public PaymentTransaction? PaymentTransaction { get; set; }

        public Order? Order { get; set; }
    }
}
