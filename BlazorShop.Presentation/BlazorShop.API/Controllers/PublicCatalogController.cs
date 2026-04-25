namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Discovery;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;

    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/public/catalog")]
    public class PublicCatalogController : ControllerBase
    {
        private readonly IPublicCatalogService _publicCatalogService;

        public PublicCatalogController(IPublicCatalogService publicCatalogService)
        {
            _publicCatalogService = publicCatalogService;
        }

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<GetCategory>>> GetCategories()
        {
            var categories = await _publicCatalogService.GetPublishedCategoriesAsync();
            return Ok(categories);
        }

        [HttpGet("sitemap")]
        public async Task<ActionResult<GetPublicCatalogSitemap>> GetSitemap()
        {
            var sitemap = await _publicCatalogService.GetPublishedSitemapAsync();
            return Ok(sitemap);
        }

        [HttpGet("categories/slug/{slug}")]
        public async Task<ActionResult<GetCategoryPage>> GetCategoryBySlug(string slug)
        {
            var categoryPage = await _publicCatalogService.GetPublishedCategoryPageBySlugAsync(slug);
            return categoryPage is null ? NotFound() : Ok(categoryPage);
        }

        [HttpGet("products")]
        public async Task<ActionResult<PagedResult<GetCatalogProduct>>> GetProducts([FromQuery] ProductCatalogQuery query)
        {
            var products = await _publicCatalogService.GetPublishedCatalogPageAsync(query);
            return Ok(products);
        }

        [HttpGet("products/slug/{slug}")]
        public async Task<ActionResult<GetProduct>> GetProductBySlug(string slug)
        {
            var product = await _publicCatalogService.GetPublishedProductBySlugAsync(slug);
            return product is null ? NotFound() : Ok(product);
        }
    }
}