namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.Services.Contracts.Admin;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/admin/audit")]
    public class AdminAuditController : ControllerBase
    {
        private readonly IAdminAuditService _adminAuditService;

        public AdminAuditController(IAdminAuditService adminAuditService)
        {
            _adminAuditService = adminAuditService;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AdminAuditQueryDto query)
        {
            return Ok(await _adminAuditService.GetAsync(query));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _adminAuditService.GetByIdAsync(id);
            return result.Success ? Ok(result.Payload) : ToFailureResult(result);
        }

        private IActionResult ToFailureResult(ServiceResponse<AdminAuditLogDto> result)
        {
            return result.ResponseType switch
            {
                ServiceResponseType.NotFound => NotFound(result),
                ServiceResponseType.ValidationError => BadRequest(result),
                _ => BadRequest(result),
            };
        }
    }
}
