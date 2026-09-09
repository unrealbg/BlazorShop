namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using System.Security.Claims;

    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Routing;

    using Moq;

    using Xunit;

    public class CartControllerTests
    {
        [Fact]
        public void Routes_ExposeAuthoritativeOrderHistoryWithoutLegacyHistoryEndpoints()
        {
            var routeTemplates = typeof(CartController)
                .GetMethods()
                .SelectMany(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true))
                .Cast<HttpMethodAttribute>()
                .Select(attribute => attribute.Template)
                .Where(template => template is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains("checkout", routeTemplates);
            Assert.Contains("user/orders", routeTemplates);
            Assert.Contains("orders", routeTemplates);
            Assert.DoesNotContain("save-checkout", routeTemplates);
            Assert.DoesNotContain("order-items", routeTemplates);
            Assert.DoesNotContain("user/order-items", routeTemplates);
        }

        [Fact]
        public async Task Checkout_ReturnsTypedResultOwnedByAuthenticatedUser()
        {
            var checkout = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
            };
            var typedResult = new CheckoutResult(
                Guid.NewGuid(),
                "COD-TEST",
                CheckoutStatus.Confirmed,
                CheckoutPaymentKind.CashOnDelivery);
            var orchestrator = new Mock<ICheckoutOrchestrator>();
            var idempotencyKey = Guid.NewGuid();
            orchestrator.Setup(service => service.CheckoutAsync(
                    checkout,
                    "authenticated-user",
                    idempotencyKey,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CheckoutExecutionResult.Succeeded(
                    new ServiceResponse<CheckoutResult>(true) { Payload = typedResult }));
            var controller = new CartController(
                orchestrator.Object,
                Mock.Of<IOrderQueryService>(),
                Mock.Of<IOrderTrackingService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "authenticated-user"),
                        ],
                        authenticationType: "TestAuth")),
                    },
                },
            };

            var result = await controller.Checkout(checkout, idempotencyKey.ToString("D"), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceResponse<CheckoutResult>>(ok.Value);
            Assert.Same(typedResult, response.Payload);
            Assert.Equal(typedResult.OrderId, response.Payload!.OrderId);
            Assert.Equal("COD-TEST", response.Payload.OrderReference);
            Assert.Equal(CheckoutStatus.Confirmed, response.Payload.Status);
            orchestrator.Verify(service => service.CheckoutAsync(
                checkout,
                "authenticated-user",
                idempotencyKey,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Checkout_ReturnsUnauthorizedWithoutServerIdentity()
        {
            var orchestrator = new Mock<ICheckoutOrchestrator>();
            var controller = new CartController(
                orchestrator.Object,
                Mock.Of<IOrderQueryService>(),
                Mock.Of<IOrderTrackingService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity()),
                    },
                },
            };
            var checkout = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                Carts = [],
            };

            var result = await controller.Checkout(checkout, Guid.NewGuid().ToString("D"), CancellationToken.None);

            Assert.IsType<UnauthorizedObjectResult>(result);
            orchestrator.Verify(service => service.CheckoutAsync(
                It.IsAny<Checkout>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-guid")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public async Task Checkout_RejectsMissingOrMalformedIdempotencyKeyBeforeOrchestration(string? key)
        {
            var orchestrator = new Mock<ICheckoutOrchestrator>();
            var controller = CreateAuthenticatedController(orchestrator.Object);

            var result = await controller.Checkout(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                    Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
                },
                key,
                CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            orchestrator.Verify(service => service.CheckoutAsync(
                It.IsAny<Checkout>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Checkout_MapsFingerprintConflictAndMarksTerminalReplay()
        {
            var checkout = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
            };
            var key = Guid.NewGuid();
            var orchestrator = new Mock<ICheckoutOrchestrator>();
            orchestrator.SetupSequence(service => service.CheckoutAsync(
                    checkout,
                    "authenticated-user",
                    key,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CheckoutExecutionResult.Conflict("Different intent"))
                .ReturnsAsync(CheckoutExecutionResult.Succeeded(
                    new ServiceResponse<CheckoutResult>(true)
                    {
                        Payload = new CheckoutResult(
                            Guid.NewGuid(),
                            "COD-REPLAY",
                            CheckoutStatus.Confirmed,
                            CheckoutPaymentKind.CashOnDelivery),
                    },
                    isReplay: true));
            var controller = CreateAuthenticatedController(orchestrator.Object);

            var conflict = await controller.Checkout(checkout, key.ToString("B"), CancellationToken.None);
            var replay = await controller.Checkout(checkout, key.ToString("D"), CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(conflict);
            Assert.IsType<OkObjectResult>(replay);
            Assert.Equal("true", controller.Response.Headers["Idempotency-Replayed"]);
        }

        private static CartController CreateAuthenticatedController(ICheckoutOrchestrator orchestrator)
        {
            return new CartController(
                orchestrator,
                Mock.Of<IOrderQueryService>(),
                Mock.Of<IOrderTrackingService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "authenticated-user"),
                        ],
                        authenticationType: "TestAuth")),
                    },
                },
            };
        }

        [Fact]
        public async Task GetUserOrders_ReturnsOkEmptyCollection_WhenUserHasNoOrders()
        {
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();

            orderQueryService
                .Setup(service => service.GetOrdersForUserAsync("user-1"))
                .ReturnsAsync(Array.Empty<GetOrder>());

            var controller = new CartController(
                Mock.Of<ICheckoutOrchestrator>(),
                orderQueryService.Object,
                trackingService.Object)
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
        public async Task GetAllOrders_ReturnsOkEmptyCollection_WhenThereAreNoOrders()
        {
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();

            orderQueryService
                .Setup(service => service.GetAllAsync())
                .ReturnsAsync(Array.Empty<GetOrder>());

            var controller = new CartController(
                Mock.Of<ICheckoutOrchestrator>(),
                orderQueryService.Object,
                trackingService.Object);

            var result = await controller.GetAllOrders();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsAssignableFrom<IEnumerable<GetOrder>>(okResult.Value);
            Assert.Empty(payload);
        }

        [Fact]
        public async Task UpdateTracking_ReturnsNotFound_WhenOrderDoesNotExist()
        {
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();
            trackingService
                .Setup(service => service.UpdateTrackingDetailsAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderTrackingTransitionResult(OrderTrackingTransitionOutcome.NotFound));

            var controller = new CartController(
                Mock.Of<ICheckoutOrchestrator>(),
                orderQueryService.Object,
                trackingService.Object);

            var result = await controller.UpdateTracking(Guid.NewGuid(), new UpdateTrackingRequest
            {
                Carrier = "UPS",
                TrackingNumber = "1Z123",
                TrackingUrl = "https://example.com/track",
            });

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdateShippingStatus_ReturnsNoContent_WhenOrderExists()
        {
            var orderQueryService = new Mock<IOrderQueryService>();
            var trackingService = new Mock<IOrderTrackingService>();
            trackingService
                .Setup(service => service.TransitionFulfillmentAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderTrackingTransitionResult(OrderTrackingTransitionOutcome.Applied));

            var controller = new CartController(
                Mock.Of<ICheckoutOrchestrator>(),
                orderQueryService.Object,
                trackingService.Object);

            var result = await controller.UpdateShippingStatus(Guid.NewGuid(), new UpdateShippingStatusRequest
            {
                FulfillmentStatus = "Shipped",
                ShippedOn = DateTime.UtcNow,
            });

            Assert.IsType<NoContentResult>(result);
        }

        [Theory]
        [InlineData(OrderTrackingTransitionOutcome.ValidationError, StatusCodes.Status400BadRequest)]
        [InlineData(OrderTrackingTransitionOutcome.NotFound, StatusCodes.Status404NotFound)]
        [InlineData(OrderTrackingTransitionOutcome.Conflict, StatusCodes.Status409Conflict)]
        public async Task UpdateShippingStatus_DistinguishesValidationNotFoundAndConflict(
            OrderTrackingTransitionOutcome outcome,
            int expectedStatusCode)
        {
            var trackingService = new Mock<IOrderTrackingService>();
            trackingService
                .Setup(service => service.TransitionFulfillmentAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderTrackingTransitionResult(outcome, "Lifecycle failure"));
            var controller = new CartController(
                Mock.Of<ICheckoutOrchestrator>(),
                Mock.Of<IOrderQueryService>(),
                trackingService.Object);

            var result = await controller.UpdateShippingStatus(
                Guid.NewGuid(),
                new UpdateShippingStatusRequest { FulfillmentStatus = "Shipped" });

            var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        }
    }
}
