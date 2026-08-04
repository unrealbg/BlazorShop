namespace BlazorShop.Application.Options
{
    public sealed class DemoOptions
    {
        public const string SectionName = "Demo";

        public bool Enabled { get; set; }

        public int LifetimeMinutes { get; set; } = 30;

        public int MaxConcurrentSessions { get; set; } = 20;

        public string CookieName { get; set; } = "blazorshop-demo-session";

        public string? CookieDomain { get; set; }

        public string HeaderName { get; set; } = "X-BlazorShop-Demo-Session";

        public string CustomerEmail { get; set; } = "demo.user@blazorshop.local";

        public string AdminEmail { get; set; } = "demo.admin@blazorshop.local";

        public string Password { get; set; } = "Demo123!";
    }
}
