namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;

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
            var userId = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var result = await _cartService.CheckoutAsync(checkout, userId);
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

        /// <summary>
        /// Get order items for the logged-in user
        /// </summary>
        /// <returns>The ordered items for the user</returns>
        [HttpGet("user/order-items")]
        [Authorize(Roles = "User, Admin")]
        public async Task<IActionResult> GetUserOrderItems()
        {
            // Извличане на userId от токена
            var userId = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized("User ID is invalid or not found.");
            }


            // Вземане на поръчките за текущия потребител
            var result = await _cartService.GetCheckoutHistoryByUserId(userId);
            return result.Any() ? this.Ok(result) : this.NotFound("No orders found for the user.");
        }
    }
}
