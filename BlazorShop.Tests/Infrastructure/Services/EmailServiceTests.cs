namespace BlazorShop.Tests.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class EmailServiceTests
    {
        [Fact]
        public async Task SendEmailAsync_WhenDeliveryIsDisabled_CompletesWithoutConnecting()
        {
            var service = new EmailService(
                Options.Create(new EmailSettings { Enabled = false }),
                Mock.Of<ILogger<EmailService>>());

            await service.SendEmailAsync("customer@example.com", "Test", "Body");
        }
    }
}
