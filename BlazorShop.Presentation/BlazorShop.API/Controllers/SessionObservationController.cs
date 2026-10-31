namespace BlazorShop.API.Controllers
{
    using BlazorShop.API.Options;
    using BlazorShop.API.SessionObservation;
    using SessionSnapshot = BlazorShop.Application.DTOs.SessionObservation;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Authentication;
    using BlazorShop.Application.Services.Contracts.Demo;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    [ApiController]
    [Route("api/internal/storefront/session")]
    [SessionObservationEndpoint]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class SessionObservationController(
        ISessionObservationService observation,
        IDemoRequestContext demoContext,
        IOptions<DemoOptions> demo,
        IOptions<ApiRuntimeOptions> runtime) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<SessionSnapshot>> Get()
        {
            Response.Headers.CacheControl = "private, no-store";
            var hasDemoCredential = Request.Cookies.ContainsKey(demo.Value.CookieName)
                || Request.Headers.ContainsKey(demo.Value.HeaderName);
            if (hasDemoCredential && (!demoContext.IsDemo || string.IsNullOrWhiteSpace(demoContext.SessionId)))
                return Ok(SessionSnapshot.Anonymous);
            if (!Request.Cookies.TryGetValue(runtime.Value.Security.RefreshTokenCookieName, out var credential))
                return Ok(SessionSnapshot.Anonymous);

            return Ok(await observation.ObserveAsync(credential));
        }
    }
}
