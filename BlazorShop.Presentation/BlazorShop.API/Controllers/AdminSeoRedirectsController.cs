namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/seo/redirects")]
    public class AdminSeoRedirectsController : ControllerBase
    {
        private readonly ISeoRedirectService _seoRedirectService;

        public AdminSeoRedirectsController(ISeoRedirectService seoRedirectService)
        {
            _seoRedirectService = seoRedirectService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SeoRedirectDto>>> GetAll()
        {
            return Ok(await _seoRedirectService.GetAllAsync());
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _seoRedirectService.GetByIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertSeoRedirectDto request)
        {
            var result = await _seoRedirectService.CreateAsync(request);

            return result.Success
                ? CreatedAtAction(nameof(GetById), new { id = result.Id }, result)
                : ToFailureResult(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpsertSeoRedirectDto request)
        {
            var result = await _seoRedirectService.UpdateAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var result = await _seoRedirectService.DeactivateAsync(id);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _seoRedirectService.DeleteAsync(id);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<SeoRedirectDto> result)
        {
            return result.ResponseType switch
            {
                ServiceResponseType.NotFound => NotFound(result),
                ServiceResponseType.Conflict => Conflict(result),
                ServiceResponseType.ValidationError => BadRequest(result),
                _ => BadRequest(result),
            };
        }
    }
}