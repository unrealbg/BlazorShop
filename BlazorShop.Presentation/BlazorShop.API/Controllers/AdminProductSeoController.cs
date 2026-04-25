namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/products")]
    public class AdminProductSeoController : ControllerBase
    {
        private readonly IProductSeoService _productSeoService;

        public AdminProductSeoController(IProductSeoService productSeoService)
        {
            _productSeoService = productSeoService;
        }

        [HttpGet("{id:guid}/seo")]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _productSeoService.GetByProductIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        [HttpPut("{id:guid}/seo")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductSeoDto request)
        {
            var result = await _productSeoService.UpdateAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<ProductSeoDto> result)
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