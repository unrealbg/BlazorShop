namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/public/seo/redirects")]
    public class PublicSeoRedirectsController : ControllerBase
    {
        private readonly ISeoRedirectResolutionService _seoRedirectResolutionService;

        public PublicSeoRedirectsController(ISeoRedirectResolutionService seoRedirectResolutionService)
        {
            _seoRedirectResolutionService = seoRedirectResolutionService;
        }

        [HttpGet("resolve")]
        public async Task<ActionResult<SeoRedirectResolutionDto>> Resolve([FromQuery] string path)
        {
            var redirect = await _seoRedirectResolutionService.ResolvePublicPathAsync(path);
            return redirect is null ? NotFound() : Ok(redirect);
        }
    }
}