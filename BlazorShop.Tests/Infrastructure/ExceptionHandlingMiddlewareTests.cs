namespace BlazorShop.Tests.Infrastructure
{
    using System.Text.Json;

    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Infrastructure.ExceptionsMiddleware;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.FileProviders;

    using Moq;

    using Xunit;

    public class ExceptionHandlingMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WhenUnhandledExceptionInProduction_ReturnsGenericMessage()
        {
            var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Sensitive internal error"));
            var context = CreateHttpContext(Environments.Production);

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            using var payload = await JsonDocument.ParseAsync(context.Response.Body);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.Equal("An unexpected error occurred. Please try again later.", payload.RootElement.GetProperty("Message").GetString());
            Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("TraceId").GetString()));
        }

        [Fact]
        public async Task InvokeAsync_WhenDbUpdateExceptionInProduction_ReturnsGenericConflictMessage()
        {
            var middleware = CreateMiddleware(_ => throw new DbUpdateException("Update failed", new Exception("duplicate key value violates unique constraint")));
            var context = CreateHttpContext(Environments.Production);

            await middleware.InvokeAsync(context);

            context.Response.Body.Position = 0;
            using var payload = await JsonDocument.ParseAsync(context.Response.Body);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
            Assert.Equal("The request could not be completed because it conflicts with existing data.", payload.RootElement.GetProperty("Message").GetString());
            Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("TraceId").GetString()));
        }

        private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
        {
            return new ExceptionHandlingMiddleware(next);
        }

        private static DefaultHttpContext CreateHttpContext(string environmentName)
        {
            var logger = new Mock<IAppLogger<ExceptionHandlingMiddleware>>();
            var services = new ServiceCollection()
                .AddSingleton(logger.Object)
                .AddSingleton<IHostEnvironment>(new TestHostEnvironment { EnvironmentName = environmentName })
                .BuildServiceProvider();

            var context = new DefaultHttpContext
            {
                RequestServices = services,
            };

            context.Response.Body = new MemoryStream();
            return context;
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;

            public string ApplicationName { get; set; } = nameof(ExceptionHandlingMiddlewareTests);

            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}