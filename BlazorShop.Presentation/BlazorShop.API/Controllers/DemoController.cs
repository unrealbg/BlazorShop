namespace BlazorShop.API.Controllers
{
    using BlazorShop.API.Demo;
    using BlazorShop.Application.DTOs.Demo;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Demo;
    using BlazorShop.Infrastructure.Demo;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.Extensions.Options;

    [Route("api/demo")]
    [ApiController]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public sealed class DemoController : ControllerBase
    {
        private readonly IDemoSessionManager _sessionManager;
        private readonly IDemoRequestContext _requestContext;
        private readonly DemoOptions _options;

        public DemoController(
            IDemoSessionManager sessionManager,
            IDemoRequestContext requestContext,
            IOptions<DemoOptions> options)
        {
            _sessionManager = sessionManager;
            _requestContext = requestContext;
            _options = options.Value;
        }

        [HttpPost("sessions")]
        [EnableRateLimiting("AuthApi")]
        public async Task<ActionResult<DemoSessionResponse>> Start(
            StartDemoSessionRequest request,
            CancellationToken cancellationToken)
        {
            if (!_sessionManager.IsEnabled)
            {
                return NotFound();
            }

            var role = NormalizeRole(request.Role);
            if (role is null)
            {
                return BadRequest(new DemoSessionResponse
                {
                    Message = "Choose either the User or Admin demo role.",
                });
            }

            try
            {
                var session = await _sessionManager.CreateAsync(cancellationToken);
                DemoSessionCookie.Append(Response, _options, session.Token);

                if (_requestContext.IsDemo)
                {
                    _requestContext.EndAfterRequest();
                }

                return Ok(new DemoSessionResponse
                {
                    Success = true,
                    Message = $"A fresh {role.ToLowerInvariant()} demo workspace is ready.",
                    Email = role == DemoSessionConstants.AdminRole ? _options.AdminEmail : _options.CustomerEmail,
                    Password = _options.Password,
                    Role = role,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                });
            }
            catch (DemoSessionCapacityException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new DemoSessionResponse { Message = exception.Message });
            }
        }

        [HttpDelete("session")]
        [EnableRateLimiting("AuthApi")]
        public IActionResult End()
        {
            if (_requestContext.IsDemo)
            {
                _requestContext.EndAfterRequest();
            }

            DemoSessionCookie.Delete(Response, _options);
            return Ok(new { success = true, message = "The demo workspace was reset." });
        }

        private static string? NormalizeRole(string? role)
        {
            if (string.Equals(role, DemoSessionConstants.AdminRole, StringComparison.OrdinalIgnoreCase))
            {
                return DemoSessionConstants.AdminRole;
            }

            if (string.Equals(role, DemoSessionConstants.CustomerRole, StringComparison.OrdinalIgnoreCase))
            {
                return DemoSessionConstants.CustomerRole;
            }

            return null;
        }
    }
}
