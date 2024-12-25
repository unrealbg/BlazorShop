namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        /// <summary>
        /// Get all products in the cart
        /// </summary>
        /// <param name="checkout">The checkout object </param>
        /// <returns>The products in the cart </returns>
        [HttpPost("checkout")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Checkout(Checkout checkout)
        {
            var result = await _cartService.CheckoutAsync(checkout);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Save the checkout history
        /// </summary>
        /// <param name="orderItems">The list of products to save </param>
        /// <returns>The result of the save </returns>
        [HttpPost("save-checkout")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> SaveCheckout(IEnumerable<CreateOrderItem> orderItems)
        {
            var result = await _cartService.SaveCheckoutHistoryAsync(orderItems);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Get all order items
        /// </summary>
        /// <returns>The ordered items </returns>
        [HttpGet("order-items")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllCheckoutHistory()
        {
            var result = await _cartService.GetOrderItemsAsync();
            return result.Any() ? this.Ok(result) : this.NotFound();
        }
    }
}
