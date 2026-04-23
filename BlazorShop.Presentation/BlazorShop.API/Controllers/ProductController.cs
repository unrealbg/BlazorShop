namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IPublicCatalogService _publicCatalogService;

        public ProductController(IProductService productService, IPublicCatalogService publicCatalogService)
        {
            _productService = productService;
            _publicCatalogService = publicCatalogService;
        }

        /// <summary>
        /// Get all products.
        /// </summary>
        /// <returns>List of products.</returns>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<GetProduct>>> GetAll()
        {
            var data = await _productService.GetAllAsync();
            return Ok(data);
        }

        /// <summary>
        /// Get a paged product catalog for list pages.
        /// </summary>
        /// <param name="query">Catalog paging, filtering and sorting options.</param>
        /// <returns>Paged catalog items.</returns>
        [HttpGet("catalog")]
        public async Task<ActionResult<PagedResult<GetCatalogProduct>>> GetCatalog([FromQuery] ProductCatalogQuery query)
        {
            var data = await _publicCatalogService.GetPublishedCatalogPageAsync(query);
            return Ok(data);
        }

        /// <summary>
        /// Get a single product by ID.
        /// </summary>
        /// <param name="id">The ID of the product.</param>
        /// <returns>A product object.</returns>
        [HttpGet("single/{id}")]
        public async Task<ActionResult<GetProduct>> GetSingle(Guid id)
        {
            var data = await _publicCatalogService.GetPublishedProductByIdAsync(id);
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
