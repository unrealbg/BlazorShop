namespace BlazorShop.API.Controllers
{
    using BlazorShop.API.Options;
    using BlazorShop.Application.DTOs;
    using System.Security.Claims;

    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Contracts.Authentication;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly ApiRuntimeOptions _runtimeOptions;

        public AuthenticationController(IAuthenticationService authenticationService, IOptions<ApiRuntimeOptions> runtimeOptions)
        {
            _authenticationService = authenticationService;
            _runtimeOptions = runtimeOptions.Value;
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
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> LoginUser(LoginUser user)
        {
            var result = await _authenticationService.LoginUser(user);

            if (!result.Success)
            {
                return this.BadRequest(SanitizeLoginResponse(result));
            }

            if (string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, new LoginResponse { Message = "Error occurred in login." });
            }

            AppendRefreshTokenCookie(result.RefreshToken);
            return this.Ok(SanitizeLoginResponse(result));
        }

        /// <summary>
        /// Refresh the token using the refresh-token cookie.
        /// </summary>
        /// <returns>The result of the refresh </returns>
        [HttpPost("refresh-token")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RefreshToken()
        {
            if (!Request.Cookies.TryGetValue(GetRefreshTokenCookieName(), out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                DeleteRefreshTokenCookie();
                return this.BadRequest(new LoginResponse { Message = "Invalid token." });
            }

            var result = await _authenticationService.ReviveToken(refreshToken);

            if (!result.Success)
            {
                DeleteRefreshTokenCookie();
                return this.BadRequest(SanitizeLoginResponse(result));
            }

            if (string.IsNullOrWhiteSpace(result.RefreshToken))
            {
                DeleteRefreshTokenCookie();
                return this.StatusCode(StatusCodes.Status500InternalServerError, new LoginResponse { Message = "Error occurred in login." });
            }

            AppendRefreshTokenCookie(result.RefreshToken);
            return this.Ok(SanitizeLoginResponse(result));
        }

        /// <summary>
        /// Logout the current browser session by revoking the refresh-token cookie.
        /// </summary>
        /// <returns>The result of the logout operation.</returns>
        [HttpPost("logout")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Logout()
        {
            Request.Cookies.TryGetValue(GetRefreshTokenCookieName(), out var refreshToken);
            var result = await _authenticationService.Logout(refreshToken ?? string.Empty);

            DeleteRefreshTokenCookie();
            return this.Ok(result);
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


        /// <summary>
        ///  Confirm the email of the user
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="token"></param>
        /// <returns> The result of the confirmation </returns>
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var response = await _authenticationService.ConfirmEmail(userId, token);

            if (!response.Success)
            {
                return this.BadRequest(response);
            }

            return this.Ok(response);
        }

        /// <summary>
        /// Update the profile of the user
        /// </summary>
        /// <param name="dto"></param>
        /// <returns> The result of the update </returns>
        [HttpPost("update-profile")]
        [Authorize(Roles = "User, Admin")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfile dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return this.Unauthorized();
            }

            var result = await _authenticationService.UpdateProfile(userId, dto);
            return result.Success ? this.Ok(result) : this.BadRequest(result);
        }

        private void AppendRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append(GetRefreshTokenCookieName(), refreshToken, CreateRefreshTokenCookieOptions());
        }

        private void DeleteRefreshTokenCookie()
        {
            Response.Cookies.Delete(GetRefreshTokenCookieName(), CreateRefreshTokenCookieOptions());
        }

        private CookieOptions CreateRefreshTokenCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = GetRefreshTokenCookieSameSiteMode(),
                IsEssential = true,
                Path = "/"
            };
        }

        private string GetRefreshTokenCookieName()
        {
            return string.IsNullOrWhiteSpace(_runtimeOptions.Security.RefreshTokenCookieName)
                ? "__Host-blazorshop-refresh"
                : _runtimeOptions.Security.RefreshTokenCookieName;
        }

        private SameSiteMode GetRefreshTokenCookieSameSiteMode()
        {
            return Enum.TryParse<SameSiteMode>(_runtimeOptions.Security.RefreshTokenCookieSameSite, ignoreCase: true, out var sameSiteMode)
                ? sameSiteMode
                : SameSiteMode.Strict;
        }

        private static LoginResponse SanitizeLoginResponse(LoginResponse response)
        {
            return response with { RefreshToken = string.Empty };
        }
    }
}
