namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Collections.Generic;

    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Configuration;
    using BlazorShop.Storefront.Options;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    using Moq;

    using Xunit;

    public class StorefrontOptionsValidatorTests
    {
        [Fact]
        public void ApiValidator_WhenProductionAndNoApiBaseUrlOrServiceDiscovery_Fails()
        {
            var validator = CreateApiValidator(Environments.Production);

            var result = validator.Validate(name: null, new StorefrontApiOptions());

            Assert.False(result.Succeeded);
            Assert.Contains(result.Failures!, failure => failure.Contains("Api:BaseUrl", StringComparison.Ordinal));
        }

        [Fact]
        public void ApiValidator_WhenServiceDiscoveryIsConfigured_SucceedsWithoutExplicitBaseUrl()
        {
            var validator = CreateApiValidator(
                Environments.Production,
                new Dictionary<string, string?>
                {
                    ["Services:apiservice:https:0"] = "https://apiservice.internal/",
                });

            var result = validator.Validate(name: null, new StorefrontApiOptions());

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void ClientAppValidator_WhenProductionAndNoConfiguredBaseUrlOrServiceDiscovery_Fails()
        {
            var validator = CreateClientAppValidator(Environments.Production);

            var result = validator.Validate(name: null, new ClientAppOptions());

            Assert.False(result.Succeeded);
            Assert.Contains(result.Failures!, failure => failure.Contains("ClientApp:BaseUrl", StringComparison.Ordinal));
        }

        [Fact]
        public void PublicUrlValidator_WhenProductionAndBaseUrlMissing_Fails()
        {
            var validator = CreatePublicUrlValidator(Environments.Production);

            var result = validator.Validate(name: null, new StorefrontPublicUrlOptions());

            Assert.False(result.Succeeded);
            Assert.Contains(result.Failures!, failure => failure.Contains("PublicUrl:BaseUrl", StringComparison.Ordinal));
        }

        private static StorefrontApiOptionsValidator CreateApiValidator(string environmentName, IDictionary<string, string?>? configurationValues = null)
        {
            return new StorefrontApiOptionsValidator(CreateConfiguration(configurationValues), CreateHostEnvironment(environmentName));
        }

        private static StorefrontClientAppOptionsValidator CreateClientAppValidator(string environmentName, IDictionary<string, string?>? configurationValues = null)
        {
            return new StorefrontClientAppOptionsValidator(CreateConfiguration(configurationValues), CreateHostEnvironment(environmentName));
        }

        private static StorefrontPublicUrlOptionsValidator CreatePublicUrlValidator(string environmentName)
        {
            return new StorefrontPublicUrlOptionsValidator(CreateHostEnvironment(environmentName));
        }

        private static IConfiguration CreateConfiguration(IDictionary<string, string?>? configurationValues = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>())
                .Build();
        }

        private static IHostEnvironment CreateHostEnvironment(string environmentName)
        {
            var hostEnvironment = new Mock<IHostEnvironment>();
            hostEnvironment.SetupGet(environment => environment.EnvironmentName).Returns(environmentName);
            return hostEnvironment.Object;
        }
    }
}