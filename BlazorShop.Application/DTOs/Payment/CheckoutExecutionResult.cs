namespace BlazorShop.Application.DTOs.Payment
{
    using BlazorShop.Application.DTOs;

    public enum CheckoutExecutionStatus
    {
        Succeeded,
        BadRequest,
        Conflict,
        InProgress,
    }

    public sealed record CheckoutExecutionResult(
        CheckoutExecutionStatus Status,
        ServiceResponse<CheckoutResult> Response,
        bool IsReplay = false)
    {
        public bool Success => Response.Success;

        public string? Message => Response.Message;

        public CheckoutResult? Payload => Response.Payload;

        public static CheckoutExecutionResult Succeeded(
            ServiceResponse<CheckoutResult> response,
            bool isReplay = false) => new(CheckoutExecutionStatus.Succeeded, response, isReplay);

        public static CheckoutExecutionResult Invalid(
            ServiceResponse<CheckoutResult> response,
            bool isReplay = false) => new(CheckoutExecutionStatus.BadRequest, response, isReplay);

        public static CheckoutExecutionResult Conflict(string message) => new(
            CheckoutExecutionStatus.Conflict,
            new ServiceResponse<CheckoutResult>(false, message)
            {
                ResponseType = ServiceResponseType.Conflict,
            });

        public static CheckoutExecutionResult InProgress(string? message = null) => new(
            CheckoutExecutionStatus.InProgress,
            new ServiceResponse<CheckoutResult>(
                false,
                message ?? "Checkout is still processing. Retry with the same Idempotency-Key.")
            {
                ResponseType = ServiceResponseType.Conflict,
            });
    }

    public sealed record PersistedCheckoutOutcome(
        int Version,
        CheckoutExecutionStatus Status,
        ServiceResponse<CheckoutResult> Response);
}
