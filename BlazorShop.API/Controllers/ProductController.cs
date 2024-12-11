namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;

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

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _productService.GetAllAsync();
            return data.Any() ? this.Ok(data) : this.NotFound(data);
        }

        [HttpGet("single/{id}")]
        public async Task<IActionResult> GetSingle(Guid id)
        {
            var data = await _productService.GetByIdAsync(id);
            return data != null ? this.Ok(data) : this.NotFound(data);
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add(CreateProduct product)
        {
            var result = await _productService.AddAsync(product);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(UpdateProduct product)
        {
            var result = await _productService.UpdateAsync(product);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productService.DeleteAsync(id);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }
    }
}
