namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/seo/settings")]
    public class AdminSeoSettingsController : ControllerBase
    {
        private readonly ISeoSettingsService _seoSettingsService;

        public AdminSeoSettingsController(ISeoSettingsService seoSettingsService)
        {
            _seoSettingsService = seoSettingsService;
        }

        [HttpGet]
        public async Task<ActionResult<SeoSettingsDto>> Get()
        {
            var settings = await _seoSettingsService.GetCurrentAsync();
            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSeoSettingsDto request)
        {
            var result = await _seoSettingsService.UpdateAsync(request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<SeoSettingsDto> result)
        {
            return result.ResponseType switch
            {
                ServiceResponseType.ValidationError => BadRequest(result),
                _ => BadRequest(result),
            };
        }
    }
}