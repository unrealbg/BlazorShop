namespace BlazorShop.API.Controllers
{
    using System.Security.Claims;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Application.Services.Contracts.Admin;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/users")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _adminUserService;

        public AdminUsersController(IAdminUserService adminUserService)
        {
            _adminUserService = adminUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AdminUserQueryDto query)
        {
            return Ok(await _adminUserService.GetUsersAsync(query));
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            return Ok(await _adminUserService.GetRolesAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _adminUserService.GetByIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        [HttpPut("{id}/roles")]
        public async Task<IActionResult> UpdateRoles(string id, [FromBody] UpdateUserRolesDto request)
        {
            var result = await _adminUserService.UpdateRolesAsync(id, request, CurrentUserId);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPost("{id}/lock")]
        public async Task<IActionResult> Lock(string id, [FromBody] UserLockRequestDto request)
        {
            var result = await _adminUserService.LockAsync(id, request, CurrentUserId);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> Unlock(string id)
        {
            var result = await _adminUserService.UnlockAsync(id);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPost("{id}/confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string id)
        {
            var result = await _adminUserService.ConfirmEmailAsync(id);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPost("{id}/require-password-change")]
        public async Task<IActionResult> RequirePasswordChange(string id)
        {
            var result = await _adminUserService.RequirePasswordChangeAsync(id);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Deactivate(string id)
        {
            var result = await _adminUserService.DeactivateAsync(id, CurrentUserId);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private IActionResult ToFailureResult(ServiceResponse<AdminUserDetailsDto> result)
        {
            return result.ResponseType switch
            {
                ServiceResponseType.NotFound => NotFound(result),
                ServiceResponseType.Conflict => Conflict(result),
                ServiceResponseType.ValidationError => BadRequest(result),
                _ => BadRequest(result),
            };
        }
    }
}
