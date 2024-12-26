namespace BlazorShop.API.Controllers
{
    using System.Security.Claims;
    using System.Web;

    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Contracts.Authentication;

    using Microsoft.AspNetCore.Authorization;
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

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="user">The user object </param>
        /// <returns>The result of the creation </returns>
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser(CreateUser user)
        {
            var result = await _authenticationService.CreateUser(user);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Login a user
        /// </summary>
        /// <param name="user">The user object </param>
        /// <returns>The result of the login </returns>
        [HttpPost("login")]
        public async Task<IActionResult> LoginUser(LoginUser user)
        {
            var result = await _authenticationService.LoginUser(user);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Refresh the token
        /// </summary>
        /// <param name="refreshToken">The refresh token </param>
        /// <returns>The result of the refresh </returns>
        [HttpGet("refreshToken/{refreshToken}")]
        public async Task<IActionResult> RefreshToken(string refreshToken)
        {
            var encodedRefreshToken = HttpUtility.UrlDecode(refreshToken);
            var result = await _authenticationService.ReviveToken(encodedRefreshToken);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        /// <summary>
        /// Change the password of the user
        /// </summary>
        /// <param name="dto"></param>
        /// <returns> The result of the change </returns>
        [HttpPost("change-password")]
        [Authorize(Roles = "User, Admin")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePassword dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            var result = await _authenticationService.ChangePassword(dto, userId);
            if (!result.Success)
            {
                return this.BadRequest(result);
            }

            return this.Ok(result);
        }
    }
}
