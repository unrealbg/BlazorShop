namespace BlazorShop.Tests.Infrastructure.Configuration
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Infrastructure.Configuration;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class EmailSettingsOptionsValidatorTests
    {
        [Fact]
        public void Validate_WhenProductionAndRequiredValuesAreMissing_ReturnsFailures()
        {
            var validator = CreateValidator(Environments.Production);

            var result = validator.Validate(
                name: null,
                new EmailSettings
                {
                    From = "<required-sender-address>",
                    SmtpServer = "",
                    Port = 0,
                    Username = "<required-smtp-username>",
                    Password = ""
                });

            var failures = Assert.IsAssignableFrom<IEnumerable<string>>(result.Failures);

            Assert.False(result.Succeeded);
            Assert.Contains(failures, failure => failure.Contains("EmailSettings:From", StringComparison.Ordinal));
            Assert.Contains(failures, failure => failure.Contains("EmailSettings:SmtpServer", StringComparison.Ordinal));
            Assert.Contains(failures, failure => failure.Contains("EmailSettings:Port", StringComparison.Ordinal));
            Assert.Contains(failures, failure => failure.Contains("EmailSettings:Username", StringComparison.Ordinal));
            Assert.Contains(failures, failure => failure.Contains("EmailSettings:Password", StringComparison.Ordinal));
        }

        [Fact]
        public void Validate_WhenDevelopment_AllowsPlaceholderValues()
        {
            var validator = CreateValidator(Environments.Development);

            var result = validator.Validate(
                name: null,
                new EmailSettings
                {
                    From = "<required-sender-address>",
                    SmtpServer = "<required-smtp-host>",
                    Port = 587,
                    Username = "<required-smtp-username>",
                    Password = "<required-smtp-password>"
                });

            Assert.True(result.Succeeded);
        }

        private static EmailSettingsOptionsValidator CreateValidator(string environmentName)
        {
            var hostEnvironment = new Mock<IHostEnvironment>();
            hostEnvironment
                .SetupGet(environment => environment.EnvironmentName)
                .Returns(environmentName);

            return new EmailSettingsOptionsValidator(hostEnvironment.Object);
        }
    }
}