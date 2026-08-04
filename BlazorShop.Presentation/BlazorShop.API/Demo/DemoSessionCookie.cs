namespace BlazorShop.API.Demo
{
    using BlazorShop.Application.Options;

    internal static class DemoSessionCookie
    {
        public static void Append(HttpResponse response, DemoOptions options, string token)
        {
            response.Cookies.Append(
                options.CookieName,
                token,
                CreateOptions(options, maxAge: TimeSpan.FromMinutes(options.LifetimeMinutes)));
        }

        public static void Delete(HttpResponse response, DemoOptions options)
        {
            response.Cookies.Delete(options.CookieName, CreateOptions(options, maxAge: null));
        }

        private static CookieOptions CreateOptions(DemoOptions options, TimeSpan? maxAge)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
                Domain = string.IsNullOrWhiteSpace(options.CookieDomain) ? null : options.CookieDomain,
                MaxAge = maxAge,
            };
        }
    }
}
