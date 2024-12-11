namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Entities;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _categoryService.GetAllAsync();
            return categories.Any() ? this.Ok(categories) : this.NotFound(categories);
        }

        [HttpGet("single/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            return category != null ? this.Ok(category) : this.NotFound(category);
        }

        [HttpPost("add")]
        public async Task<IActionResult> Add(CreateCategory category)
        {
            var result = await _categoryService.AddAsync(category);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(UpdateCategory category)
        {
            var result = await _categoryService.UpdateAsync(category);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _categoryService.DeleteAsync(id);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }
    }
}
