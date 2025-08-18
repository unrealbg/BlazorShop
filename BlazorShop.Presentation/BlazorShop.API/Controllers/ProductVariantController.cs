namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/product")]
    [ApiController]
    public class ProductVariantController : ControllerBase
    {
        private readonly IProductVariantService _variantService;

        public ProductVariantController(IProductVariantService variantService)
        {
            _variantService = variantService;
        }

        /// <summary>
        /// Get all variants for a product
        /// </summary>
        [HttpGet("{productId}/variants")]
        public async Task<ActionResult<IEnumerable<GetProductVariant>>> GetByProductId(Guid productId)
        {
            var data = await _variantService.GetByProductIdAsync(productId);
            return data.Any() ? Ok(data) : NotFound();
        }

        /// <summary>
        /// Add variant to product
        /// </summary>
        [HttpPost("{productId}/variants")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add(Guid productId, CreateProductVariant variant)
        {
            variant.ProductId = productId;
            var result = await _variantService.AddAsync(variant);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Update product variant
        /// </summary>
        [HttpPut("variants")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(UpdateProductVariant variant)
        {
            var result = await _variantService.UpdateAsync(variant);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Delete a product variant by id
        /// </summary>
        [HttpDelete("variants/{variantId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid variantId)
        {
            var result = await _variantService.DeleteAsync(variantId);
            return result.Success ? Ok(result) : BadRequest(result.Message);
        }
    }
}
