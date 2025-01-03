﻿namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Get all categories.
        /// </summary>
        /// <returns>List of categories.</returns>
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<GetCategory>>> GetAll()
        {
            var categories = await _categoryService.GetAllAsync();
            return categories.Any() ? this.Ok(categories) : this.NotFound();
        }

        /// <summary>
        /// Get a single category by ID.
        /// </summary>
        /// <param name="id">The ID of the category.</param>
        /// <returns>A category object.</returns>
        [HttpGet("single/{id}")]
        public async Task<ActionResult<GetCategory>> GetById(Guid id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            return category != null ? this.Ok(category) : this.NotFound();
        }

        /// <summary>
        /// Add a new category.
        /// </summary>
        /// <param name="category">The category object to add.</param>
        /// <returns>The result of the operation.</returns>
        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add(CreateCategory category)
        {
            var result = await _categoryService.AddAsync(category);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Update an existing category.
        /// </summary>
        /// <param name="category">The category object with updated information.</param>
        /// <returns>The result of the operation.</returns>
        [HttpPut("update")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(UpdateCategory category)
        {
            var result = await _categoryService.UpdateAsync(category);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Delete a category by ID.
        /// </summary>
        /// <param name="id">The ID of the category to delete.</param>
        /// <returns>The result of the operation.</returns>
        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _categoryService.DeleteAsync(id);
            return result.Success ? this.Ok(result) : this.BadRequest(result.Message);
        }

        /// <summary>
        /// Get all products by category.
        /// </summary>
        /// <param name="categoryId">The ID of the category. </param>
        /// <returns>List of products by category.</returns>
        [HttpGet("products-by-category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<GetProduct>>> GetProductsByCategory(Guid categoryId)
        {
            var results = await _categoryService.GetProductsByCategoryAsync(categoryId);
            return results.Any() ? this.Ok(results) : this.NotFound();
        }
    }
}