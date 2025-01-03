namespace BlazorShop.Tests
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Aspire.Hosting;
    using Aspire.Hosting.ApplicationModel;
    using Aspire.Hosting.Testing;

    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    public abstract class ApiTestBase : IAsyncLifetime
    {
        protected DistributedApplication App { get; private set; }
        protected ResourceNotificationService ResourceNotificationService { get; private set; }
        protected HttpClient HttpClient { get; private set; }

        public async Task InitializeAsync()
        {
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BlazorShop_AppHost>();

            appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
                {
                    clientBuilder.AddStandardResilienceHandler();
                });

            this.App = await appHost.BuildAsync();
            this.ResourceNotificationService = this.App.Services.GetRequiredService<ResourceNotificationService>();
            await this.App.StartAsync();

            this.HttpClient = this.App.CreateHttpClient("apiservice");
            await this.ResourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        }

        public async Task DisposeAsync()
        {
            if (this.App != null)
            {
                await this.App.DisposeAsync();
            }
        }
    }
}