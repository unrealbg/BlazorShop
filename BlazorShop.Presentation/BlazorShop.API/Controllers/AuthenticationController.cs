namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Contracts.Authentication;

    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthenticationController(IAuthenticationService authenticationService)
        {
            _authenticationService = authenticationService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateUser(CreateUser user)
        {
            var result = await _authenticationService.CreateUser(user);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginUser(LoginUser user)
        {
            var result = await _authenticationService.LoginUser(user);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        [HttpGet("refreshToken/{refreshToken}")]
        public async Task<IActionResult> RefreshToken(string refreshToken)
        {
            var result = await _authenticationService.ReviveToken(refreshToken);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

    }
}
