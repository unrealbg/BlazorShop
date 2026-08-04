namespace BlazorShop.Application.DTOs.Demo
{
    public sealed class StartDemoSessionRequest
    {
        public string Role { get; set; } = "User";
    }

    public sealed class DemoSessionResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }
    }
}
