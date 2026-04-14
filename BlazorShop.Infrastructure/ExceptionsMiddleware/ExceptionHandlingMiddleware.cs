namespace BlazorShop.Infrastructure.ExceptionsMiddleware
{
    using System.Text.Json;

    using BlazorShop.Application.Services.Contracts.Logging;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public class ExceptionHandlingMiddleware
    {
        private const string ConflictErrorMessage = "The request could not be completed because it conflicts with existing data.";
        private const string UnexpectedErrorMessage = "An unexpected error occurred. Please try again later.";

        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DbUpdateException ex)
            {
                var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
                var hostEnvironment = context.RequestServices.GetRequiredService<IHostEnvironment>();
                logger.LogError(ex, "A database update error occurred.");

                var message = hostEnvironment.IsDevelopment()
                    ? ex.InnerException?.Message ?? ex.Message
                    : ConflictErrorMessage;

                await this.WriteJsonResponse(context, StatusCodes.Status409Conflict, message, context.TraceIdentifier);
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
                var hostEnvironment = context.RequestServices.GetRequiredService<IHostEnvironment>();
                logger.LogError(ex, "An error occurred.");

                var message = hostEnvironment.IsDevelopment()
                    ? $"An error occurred: {ex.Message}"
                    : UnexpectedErrorMessage;

                await this.WriteJsonResponse(context, StatusCodes.Status500InternalServerError, message, context.TraceIdentifier);
            }
        }

        private async Task WriteJsonResponse(HttpContext context, int statusCode, string message, string traceId)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = new
            {
                StatusCode = statusCode,
                Message = message,
                TraceId = traceId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
