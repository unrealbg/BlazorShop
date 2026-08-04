namespace BlazorShop.Application.Services.Contracts.Demo
{
    public interface IDemoRequestContext
    {
        bool IsDemo { get; }

        string? SessionId { get; }

        void EndAfterRequest();
    }

    public static class DemoSessionConstants
    {
        public const string ClaimType = "blazorshop_demo_session";

        public const string CustomerRole = "User";

        public const string AdminRole = "Admin";
    }
}
