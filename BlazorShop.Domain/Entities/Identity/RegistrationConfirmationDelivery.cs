namespace BlazorShop.Domain.Entities.Identity;

public enum ConfirmationDeliveryState { Pending, Processing, Delivered, DeadLetter, Suppressed }

// One durable business identity per account, with bounded, explicitly requested generations.
// Contains no password, confirmation token, link or message body.
public sealed class RegistrationConfirmationDelivery
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string EmailSnapshot { get; set; } = string.Empty;
    public string SecurityStampHash { get; set; } = string.Empty;
    public int Generation { get; set; } = 1;
    public ConfirmationDeliveryState State { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
    public DateTime LastRequestedOn { get; set; }
    public DateTime RequestWindowStartedOn { get; set; }
    public int RequestsInWindow { get; set; } = 1;
    public DateTime NextAttemptOn { get; set; }
    public DateTime? LastAttemptOn { get; set; }
    public DateTime? DeliveredOn { get; set; }
    public Guid? LeaseOwnerId { get; set; }
    public DateTime? LeaseExpiresOn { get; set; }
    public string? LastFailureCode { get; set; }
}
