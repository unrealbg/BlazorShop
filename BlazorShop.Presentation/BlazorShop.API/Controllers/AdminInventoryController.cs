namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Inventory;
    using BlazorShop.Application.Services.Contracts.Admin;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/inventory")]
    public class AdminInventoryController : ControllerBase
    {
        private readonly IAdminInventoryService _adminInventoryService;

        public AdminInventoryController(IAdminInventoryService adminInventoryService)
        {
            _adminInventoryService = adminInventoryService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AdminInventoryQueryDto query)
        {
            return Ok(await _adminInventoryService.GetAsync(query));
        }

        [HttpPut("product/{productId:guid}")]
        public async Task<IActionResult> UpdateProductStock(Guid productId, [FromBody] UpdateProductStockDto request)
        {
            var result = await _adminInventoryService.UpdateProductStockAsync(productId, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPut("variant/{variantId:guid}")]
        public async Task<IActionResult> UpdateVariantStock(Guid variantId, [FromBody] UpdateVariantStockDto request)
        {
            var result = await _adminInventoryService.UpdateVariantStockAsync(variantId, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult<TPayload>(ServiceResponse<TPayload> result)
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
