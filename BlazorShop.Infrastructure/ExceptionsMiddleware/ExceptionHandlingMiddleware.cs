namespace BlazorShop.Infrastructure.ExceptionsMiddleware;

using System.Text.Json;
using BlazorShop.Application.Services.Contracts.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
                logger.LogError(innerException, "A database update error occurred. TraceId: {TraceId}", context.TraceIdentifier);

                var problemDetails = innerException.Number switch
                {
                    515 => CreateProblemDetails(
                        context,
                        StatusCodes.Status400BadRequest,
                        "Validation Error",
                        "Some required fields are missing.",
                        "https://tools.ietf.org/html/rfc7231#section-6.5.1"),
                    547 => CreateProblemDetails(
                        context,
                        StatusCodes.Status409Conflict,
                        "Constraint Violation",
                        "Foreign key constraint violation.",
                        "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
                    2601 or 2627 => CreateProblemDetails(
                        context,
                        StatusCodes.Status409Conflict,
                        "Duplicate Record",
                        "This record already exists in the database.",
                        "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
                    _ => CreateProblemDetails(
                        context,
                        StatusCodes.Status500InternalServerError,
                        "Database Error",
                        "An unexpected database error occurred.",
                        "https://tools.ietf.org/html/rfc7231#section-6.6.1")
                };

                await WriteProblemDetailsResponse(context, problemDetails);
            }
            else
            {
                logger.LogError(ex, "A database update error occurred. TraceId: {TraceId}", context.TraceIdentifier);
                var problemDetails = CreateProblemDetails(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "Database Error",
                    "A database update error occurred.",
                    "https://tools.ietf.org/html/rfc7231#section-6.6.1");

                await WriteProblemDetailsResponse(context, problemDetails);
            }
        }
        catch (ArgumentException ex)
        {
            var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
            logger.LogWarning(ex, "Invalid argument provided. TraceId: {TraceId}", context.TraceIdentifier);

            var problemDetails = CreateProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                ex.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1");

            await WriteProblemDetailsResponse(context, problemDetails);
        }
        catch (UnauthorizedAccessException ex)
        {
            var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
            logger.LogWarning(ex, "Unauthorized access attempt. TraceId: {TraceId}", context.TraceIdentifier);

            var problemDetails = CreateProblemDetails(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "You are not authorized to access this resource.",
                "https://tools.ietf.org/html/rfc7235#section-3.1");

            await WriteProblemDetailsResponse(context, problemDetails);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<IAppLogger<ExceptionHandlingMiddleware>>();
            logger.LogError(ex, "An unexpected error occurred. TraceId: {TraceId}", context.TraceIdentifier);

            var problemDetails = CreateProblemDetails(
                context,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request.",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1");

            await WriteProblemDetailsResponse(context, problemDetails);
        }
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? type = null)
    {
        return new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = context.TraceIdentifier,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };
    }

    private static async Task WriteProblemDetailsResponse(HttpContext context, ProblemDetails problemDetails)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}
