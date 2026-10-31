namespace BlazorShop.API.Demo
{
    using System.IdentityModel.Tokens.Jwt;

    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Demo;
    using BlazorShop.Infrastructure.Demo;
    using BlazorShop.API.SessionObservation;

    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Mvc;

    public sealed class DemoSessionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DemoSessionMiddleware> _logger;

        public DemoSessionMiddleware(RequestDelegate next, ILogger<DemoSessionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext httpContext,
            IDemoSessionManager sessionManager,
            DemoRequestContext requestContext,
            IOptions<DemoOptions> optionsAccessor)
        {
            var options = optionsAccessor.Value;
            // This unvalidated marker can only deny a request; JWT authentication validates and binds it later.
            var tokenSessionId = ReadDemoSessionClaim(httpContext.Request.Headers.Authorization);
            if (!options.Enabled)
            {
                if (tokenSessionId is not null)
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "Demo access tokens require an active demo workspace.");
                    return;
                }

                await _next(httpContext);
                return;
            }

            var cookieToken = httpContext.Request.Cookies[options.CookieName];
            var headerToken = httpContext.Request.Headers[options.HeaderName].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(cookieToken)
                && !string.IsNullOrWhiteSpace(headerToken)
                && !string.Equals(cookieToken, headerToken, StringComparison.Ordinal))
            {
                await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "Demo session mismatch.");
                return;
            }

            var token = !string.IsNullOrWhiteSpace(headerToken) ? headerToken : cookieToken;

            if (string.IsNullOrWhiteSpace(token))
            {
                if (tokenSessionId is not null)
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "The demo session cookie is missing.");
                    return;
                }

                await _next(httpContext);
                return;
            }

            var observation = httpContext.GetEndpoint()?.Metadata.GetMetadata<SessionObservationEndpointAttribute>() is not null;
            var lease = await sessionManager.AcquireAsync(token, httpContext.RequestAborted, readOnly: observation);
            if (lease is null)
            {
                if (!observation) DemoSessionCookie.Delete(httpContext.Response, options);

                if (CanRecoverWithoutSession(httpContext.Request))
                {
                    await _next(httpContext);
                    return;
                }

                await WriteProblemAsync(httpContext, StatusCodes.Status410Gone, "This demo workspace has expired. Start a new demo session.");
                return;
            }

            await using (lease)
            {
                if (tokenSessionId is not null
                    && (string.IsNullOrWhiteSpace(tokenSessionId)
                        || !string.Equals(tokenSessionId, lease.Context.Id, StringComparison.Ordinal)))
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "The access token belongs to another demo session.");
                    return;
                }

                using var activation = requestContext.Activate(lease.Context);
                var shouldEndSession = false;

                try
                {
                    await _next(httpContext);
                }
                finally
                {
                    shouldEndSession = requestContext.ShouldEndAfterRequest;
                }

                if (shouldEndSession)
                {
                    httpContext.Response.OnCompleted(
                        async () =>
                        {
                            try
                            {
                                await sessionManager.EndAsync(token);
                            }
                            catch (Exception exception)
                            {
                                _logger.LogError(exception, "Failed to reset demo session after the request completed.");
                            }
                        });
                }
            }
        }

        private static bool CanRecoverWithoutSession(HttpRequest request)
        {
            return (HttpMethods.IsPost(request.Method) && request.Path.Equals("/api/demo/sessions"))
                || (HttpMethods.IsDelete(request.Method) && request.Path.Equals("/api/demo/session"));
        }

        private static string? ReadDemoSessionClaim(string? authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader)
                || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                var token = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeader["Bearer ".Length..].Trim());
                var claims = token.Claims.Where(claim => claim.Type == DemoSessionConstants.ClaimType).Take(2).ToArray();
                return claims.Length switch
                {
                    0 => null,
                    1 => claims[0].Value,
                    _ => string.Empty,
                };
            }
            catch
            {
                return null;
            }
        }

        private static Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
        {
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode == StatusCodes.Status410Gone ? "Demo workspace expired" : "Unauthorized demo session",
                Detail = detail,
            });
        }
    }
}
