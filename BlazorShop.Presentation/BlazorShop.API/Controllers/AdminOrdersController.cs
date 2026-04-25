namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Orders;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Admin;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/orders")]
    public class AdminOrdersController : ControllerBase
    {
        private readonly IAdminOrderService _adminOrderService;

        public AdminOrdersController(IAdminOrderService adminOrderService)
        {
            _adminOrderService = adminOrderService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AdminOrderQueryDto query)
        {
            return Ok(await _adminOrderService.GetAsync(query));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _adminOrderService.GetByIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        [HttpPut("{id:guid}/tracking")]
        public async Task<IActionResult> UpdateTracking(Guid id, [FromBody] UpdateTrackingRequest request)
        {
            var result = await _adminOrderService.UpdateTrackingAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPut("{id:guid}/shipping-status")]
        public async Task<IActionResult> UpdateShippingStatus(Guid id, [FromBody] UpdateShippingStatusRequest request)
        {
            var result = await _adminOrderService.UpdateShippingStatusAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        [HttpPut("{id:guid}/admin-note")]
        public async Task<IActionResult> UpdateAdminNote(Guid id, [FromBody] UpdateOrderAdminNoteRequest request)
        {
            var result = await _adminOrderService.UpdateAdminNoteAsync(id, request);
            return result.Success ? Ok(result) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<GetOrder> result)
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
