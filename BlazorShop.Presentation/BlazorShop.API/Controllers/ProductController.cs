namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Get all products.
        /// </summary>
        /// <returns>List of products.</returns>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<GetProduct>>> GetAll()
        {
            var data = await _productService.GetAllAsync();
            return data.Any() ? Ok(data) : NotFound();
        }

        /// <summary>
        /// Get a single product by ID.
        /// </summary>
        /// <param name="id">The ID of the product.</param>
        /// <returns>A product object.</returns>
        [HttpGet("single/{id}")]
        public async Task<ActionResult<GetProduct>> GetSingle(Guid id)
        {
            var data = await _productService.GetByIdAsync(id);
            return data != null ? this.Ok(data) : this.NotFound();
        }

        /// <summary>
        /// Add a new product.
        /// </summary>
        /// <param name="product">The product object to add.</param>
        /// <returns>The result of the operation.</returns>
        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add(CreateProduct product)
        {
            var result = await _productService.AddAsync(product);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Update an existing product.
        /// </summary>
        /// <param name="product">The product object with updated information.</param>
        /// <returns>The result of the operation.</returns>
        [HttpPut("update")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(UpdateProduct product)
        {
            var result = await _productService.UpdateAsync(product);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Delete a product by ID.
        /// </summary>
        /// <param name="id">The ID of the product to delete.</param>
        /// <returns>The result of the operation.</returns>
        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productService.DeleteAsync(id);
            return result.Success ? this.Ok(result) : this.BadRequest(result.Message);
        }
    }
}
