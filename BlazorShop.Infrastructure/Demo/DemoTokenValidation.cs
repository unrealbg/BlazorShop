namespace BlazorShop.Infrastructure.Demo
{
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Demo;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    internal static class DemoTokenValidation
    {
        public static Task ValidateAsync(TokenValidatedContext context)
        {
            var session = context.HttpContext.RequestServices.GetRequiredService<DemoRequestContext>().Current;
            var claims = context.Principal!.FindAll(DemoSessionConstants.ClaimType).ToArray();
            if (claims.Length == 0 && session is null)
            {
                return Task.CompletedTask;
            }

            var enabled = context.HttpContext.RequestServices.GetRequiredService<IOptions<DemoOptions>>().Value.Enabled;
            if (!enabled || session is null || session.ExpiresAtUtc <= DateTime.UtcNow
                || claims.Length != 1 || string.IsNullOrWhiteSpace(claims[0].Value)
                || !string.Equals(claims[0].Value, session.Id, StringComparison.Ordinal))
            {
                context.Fail("The access token requires its active demo session.");
            }

            return Task.CompletedTask;
        }
    }
}
