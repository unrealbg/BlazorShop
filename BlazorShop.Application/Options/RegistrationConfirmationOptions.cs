namespace BlazorShop.Application.Options;

public sealed class RegistrationConfirmationOptions
{
    public const string SectionName = "RegistrationConfirmation";
    public bool WorkerEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 5;
    public int InitialRetrySeconds { get; set; } = 30;
    public int MaxRetrySeconds { get; set; } = 900;
    public int ResendCooldownSeconds { get; set; } = 300;
    public int RequestWindowHours { get; set; } = 24;
    public int MaxRequestsPerWindow { get; set; } = 5;
}
