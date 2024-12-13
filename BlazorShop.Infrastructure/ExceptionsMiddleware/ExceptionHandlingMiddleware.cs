namespace BlazorShop.Infrastructure.ExceptionsMiddleware
{
    using System.Text.Json;

    using BlazorShop.Application.Services.Contracts.Logging;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class ExceptionHandlingMiddleware
    {
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

                if (ex.InnerException is SqlException innerException)
                {
                    logger.LogError(innerException, "A database update error occurred.");

                    switch (innerException.Number)
                    {
                        case 515:
                            await this.WriteJsonResponse(context, StatusCodes.Status400BadRequest, "Some required fields are missing.");
                            break;
                        case 547:
                            await this.WriteJsonResponse(context, StatusCodes.Status409Conflict, "Foreign key constraint violation.");
                            break;
                        case 2601:
                        case 2627:
                            await this.WriteJsonResponse(context, StatusCodes.Status409Conflict, "This record already exists in the database.");
                            break;
                        default:
                            await this.WriteJsonResponse(context, StatusCodes.Status500InternalServerError, "An unexpected database error occurred.");
                            break;
                    }
                }
                else
                {
                    logger.LogError(ex, "A database update error occurred.");
                    await this.WriteJsonResponse(context, StatusCodes.Status500InternalServerError, "A database update error occurred.");
                }
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
                logger.LogError(ex, "An error occurred.");

                await this.WriteJsonResponse(context, StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }

        private async Task WriteJsonResponse(HttpContext context, int statusCode, string message)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = new
            {
                StatusCode = statusCode,
                Message = message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
