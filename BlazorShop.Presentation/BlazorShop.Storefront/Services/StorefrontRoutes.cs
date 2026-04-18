namespace BlazorShop.Storefront.Services
{
    public static class StorefrontRoutes
    {
        public const string Home = "/";
        public const string About = "/about-us";
        public const string Faq = "/faq";
        public const string Privacy = "/privacy";
        public const string Terms = "/terms";
        public const string CustomerService = "/customer-service";
        public const string NewReleases = "/new-releases";
        public const string TodaysDeals = "/todays-deals";

        public static string Category(string? slug)
        {
            return string.IsNullOrWhiteSpace(slug)
                ? "/category"
                : $"/category/{Uri.EscapeDataString(slug.Trim())}";
        }

        public static string Product(string? slug)
        {
            return string.IsNullOrWhiteSpace(slug)
                ? "/product"
                : $"/product/{Uri.EscapeDataString(slug.Trim())}";
        }
    }
}