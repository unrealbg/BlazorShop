namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using System.Security.Claims;

    using BlazorShop.API.Controllers;
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
    }
}