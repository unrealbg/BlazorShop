namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Settings;
    using BlazorShop.Application.Services.Contracts.Admin;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/settings")]
    public class AdminSettingsController : ControllerBase
    {
        private readonly IAdminSettingsService _adminSettingsService;

        public AdminSettingsController(IAdminSettingsService adminSettingsService)
        {
            _adminSettingsService = adminSettingsService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(await _adminSettingsService.GetAsync());
        }

        [HttpPut("store")]
        public async Task<IActionResult> UpdateStore([FromBody] UpdateStoreSettingsDto request)
        {
            var result = await _adminSettingsService.UpdateStoreAsync(request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPut("orders")]
        public async Task<IActionResult> UpdateOrders([FromBody] UpdateOrderSettingsDto request)
        {
            var result = await _adminSettingsService.UpdateOrdersAsync(request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPut("notifications")]
        public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationSettingsDto request)
        {
            var result = await _adminSettingsService.UpdateNotificationsAsync(request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult<TPayload>(ServiceResponse<TPayload> result)
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
