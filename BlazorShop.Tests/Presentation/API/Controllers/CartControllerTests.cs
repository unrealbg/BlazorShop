namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using System.Security.Claims;

    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class CartControllerTests
    {
        [Fact]
        public async Task SaveCheckout_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
        {
            var cartService = new Mock<ICartService>();
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();

            var controller = new CartController(cartService.Object, orderQueryService.Object, trackingService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity())
                    }
                }
            };

            var result = await controller.SaveCheckout(Array.Empty<CreateOrderItem>());

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("User ID is invalid or not found.", unauthorized.Value);
            cartService.Verify(service => service.SaveCheckoutHistoryAsync(It.IsAny<string>(), It.IsAny<IEnumerable<CreateOrderItem>>()), Times.Never);
        }

        [Fact]
        public async Task SaveCheckout_UsesAuthenticatedUserIdInsteadOfClientPayload()
        {
            var cartService = new Mock<ICartService>();
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();
            var orderItems = new[]
            {
                new CreateOrderItem
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    UserId = "spoofed-user",
                }
            };

            cartService
                .Setup(service => service.SaveCheckoutHistoryAsync("user-1", orderItems))
                .ReturnsAsync(new ServiceResponse(true, "Checkout history saved successfully"));

            var controller = new CartController(cartService.Object, orderQueryService.Object, trackingService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        ],
                        authenticationType: "TestAuth"))
                    }
                }
            };

            var result = await controller.SaveCheckout(orderItems);

            Assert.IsType<OkObjectResult>(result);
            cartService.Verify(service => service.SaveCheckoutHistoryAsync("user-1", orderItems), Times.Once);
        }

        [Fact]
        public async Task GetUserOrders_ReturnsOkEmptyCollection_WhenUserHasNoOrders()
        {
            var cartService = new Mock<ICartService>();
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();

            orderQueryService
                .Setup(service => service.GetOrdersForUserAsync("user-1"))
                .ReturnsAsync(Array.Empty<GetOrder>());

            var controller = new CartController(cartService.Object, orderQueryService.Object, trackingService.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "user-1"),
                        ],
                        authenticationType: "TestAuth"))
                    }
                }
            };

            var result = await controller.GetUserOrders();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsAssignableFrom<IEnumerable<GetOrder>>(okResult.Value);
            Assert.Empty(payload);
        }

        [Fact]
        public async Task UpdateTracking_ReturnsNotFound_WhenOrderDoesNotExist()
        {
            var cartService = new Mock<ICartService>();
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();
            trackingService
                .Setup(service => service.UpdateTrackingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            var controller = new CartController(cartService.Object, orderQueryService.Object, trackingService.Object);

            var result = await controller.UpdateTracking(Guid.NewGuid(), new UpdateTrackingRequest
            {
                Carrier = "UPS",
                TrackingNumber = "1Z123",
                TrackingUrl = "https://example.com/track",
            });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdateShippingStatus_ReturnsNoContent_WhenOrderExists()
        {
            var cartService = new Mock<ICartService>();
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();
            trackingService
                .Setup(service => service.UpdateShippingStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(true);

            var controller = new CartController(cartService.Object, orderQueryService.Object, trackingService.Object);

            var result = await controller.UpdateShippingStatus(Guid.NewGuid(), new UpdateShippingStatusRequest
            {
                ShippingStatus = "Shipped",
                ShippedOn = DateTime.UtcNow,
            });

            Assert.IsType<NoContentResult>(result);
        }
    }
}