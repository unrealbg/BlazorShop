namespace BlazorShop.API.Controllers
{
    using System.Security.Claims;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly IOrderQueryService _orderQueryService;
        private readonly IOrderTrackingService _trackingService;

        public CartController(ICartService cartService, IOrderQueryService orderQueryService, IOrderTrackingService trackingService)
        {
            _cartService = cartService;
            _orderQueryService = orderQueryService;
            _trackingService = trackingService;
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

        /// <summary>
        /// Get orders for the logged-in user (real Orders)
        /// </summary>
        /// <returns>The orders for the user</returns>
        [HttpGet("user/orders")]
        [Authorize(Roles = "User, Admin")]
        public async Task<IActionResult> GetUserOrders()
        {
            // Извличане на userId от токена
            var userId = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized("User ID is invalid or not found.");
            }

            // Вземане на поръчките за текущия потребител (чрез новата услуга)
            var result = await _orderQueryService.GetOrdersForUserAsync(userId);
            return result.Any() ? this.Ok(result) : this.NotFound("No orders found for the user.");
        }

        /// <summary>
        /// Get all orders (real Orders)
        /// </summary>
        /// <returns>All orders</returns>
        [HttpGet("orders")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            // Вземане на всички поръчки (чрез новата услуга)
            var result = await _orderQueryService.GetAllAsync();
            return result.Any() ? this.Ok(result) : this.NotFound();
        }

        /// <summary>
        /// Update the tracking information for an order
        /// </summary>
        /// <param name="orderId">The ID of the order to update</param>
        /// <param name="dto">The tracking information</param>
        /// <returns>No Content response</returns>
        [HttpPut("orders/{orderId:guid}/tracking")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTracking(Guid orderId, UpdateTrackingRequest dto)
        {
            await _trackingService.UpdateTrackingAsync(orderId, dto.Carrier, dto.TrackingNumber, dto.TrackingUrl);
            return NoContent();
        }

        /// <summary>
        /// Update the shipping status of an order
        /// </summary>
        /// <param name="orderId">The ID of the order to update</param>
        /// <param name="dto">The shipping status information</param>
        /// <returns>No Content response</returns>
        [HttpPut("orders/{orderId:guid}/shipping-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateShippingStatus(Guid orderId, UpdateShippingStatusRequest dto)
        {
            await _trackingService.UpdateShippingStatusAsync(orderId, dto.ShippingStatus, dto.ShippedOn, dto.DeliveredOn);
            return NoContent();
        }
    }
}
