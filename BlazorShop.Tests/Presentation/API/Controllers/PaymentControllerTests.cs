namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.Services.Contracts.Payment;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class PaymentControllerTests
    {
        [Fact]
        public async Task CapturePayPal_WhenCaptureFails_RedirectsToCancelPage()
        {
            var paymentMethodService = new Mock<IPaymentMethodService>();
            var payPalPaymentService = new Mock<IPayPalPaymentService>();
            payPalPaymentService
                .Setup(service => service.CaptureAsync("demo-token"))
                .ReturnsAsync(false);

            var controller = CreateController(paymentMethodService.Object, payPalPaymentService.Object);

            var result = await controller.CapturePayPal("demo-token");

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://shop.example.com/payment-cancel", redirect.Url);
        }

        private static PaymentController CreateController(
            IPaymentMethodService paymentMethodService,
            IPayPalPaymentService payPalPaymentService)
        {
            var controller = new PaymentController(
                paymentMethodService,
                payPalPaymentService,
                Options.Create(new BlazorShop.Application.Options.ClientAppOptions
                {
                    BaseUrl = "https://shop.example.com"
                }));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };

            return controller;
        }
    }
}