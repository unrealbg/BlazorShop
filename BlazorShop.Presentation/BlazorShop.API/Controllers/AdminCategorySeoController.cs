namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/categories")]
    public class AdminCategorySeoController : ControllerBase
    {
        private readonly ICategorySeoService _categorySeoService;

        public AdminCategorySeoController(ICategorySeoService categorySeoService)
        {
            _categorySeoService = categorySeoService;
        }

        [HttpGet("{id:guid}/seo")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _categorySeoService.GetByCategoryIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        [HttpPut("{id:guid}/seo")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategorySeoDto request)
        {
            var result = await _categorySeoService.UpdateAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<CategorySeoDto> result)
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