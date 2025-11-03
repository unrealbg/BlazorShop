namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class ProductRecommendationController : ControllerBase
    {
        private readonly IProductRecommendationService _recommendationService;
        private readonly ILogger<ProductRecommendationController> _logger;

        public ProductRecommendationController(
            IProductRecommendationService recommendationService,
            ILogger<ProductRecommendationController> logger)
        {
            _recommendationService = recommendationService;
            _logger = logger;
        }

        /// <summary>
        /// Get product recommendations for a given product ID.
        /// </summary>
        /// <param name="productId">The ID of the product to get recommendations for.</param>
        /// <returns>List of recommended products.</returns>
        /// <response code="200">Returns the list of recommendations</response>
        /// <response code="400">If the product ID is invalid</response>
        /// <response code="404">If no recommendations are found</response>
        /// <response code="500">If an internal server error occurs</response>
        [HttpGet("{productId}")]
        [ProducesResponseType(typeof(IEnumerable<GetProductRecommendation>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<GetProductRecommendation>>> GetRecommendations(Guid productId)
        {
            try
            {
                if (productId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid product ID provided: {ProductId}", productId);
                    return BadRequest(new { error = "Invalid product ID" });
                }

                _logger.LogInformation("Fetching recommendations for product: {ProductId}", productId);

                var recommendations = await _recommendationService.GetRecommendationsForProductAsync(productId);

                if (!recommendations.Any())
                {
                    _logger.LogInformation("No recommendations found for product: {ProductId}", productId);
                    return NotFound(new { message = "No recommendations found for this product" });
                }

                _logger.LogInformation("Successfully returned {Count} recommendations for product: {ProductId}",
                    recommendations.Count(), productId);

                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching recommendations for product: {ProductId}", productId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An error occurred while processing your request" });
            }
        }
    }
}
