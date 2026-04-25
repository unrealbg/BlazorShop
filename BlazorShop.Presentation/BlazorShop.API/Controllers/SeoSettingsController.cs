namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/seo/settings")]
    public class SeoSettingsController : ControllerBase
    {
        private readonly ISeoSettingsService _seoSettingsService;

        public SeoSettingsController(ISeoSettingsService seoSettingsService)
        {
            _seoSettingsService = seoSettingsService;
        }

        [HttpGet]
        public async Task<ActionResult<SeoSettingsDto>> Get()
        {
            var settings = await _seoSettingsService.GetCurrentAsync();
            return Ok(settings);
        }
    }
}