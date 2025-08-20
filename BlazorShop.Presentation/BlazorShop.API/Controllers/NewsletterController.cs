namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.Services.Contracts;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class NewsletterController : ControllerBase
    {
        private readonly INewsletterService _newsletter;

        public NewsletterController(INewsletterService newsletter)
        {
            _newsletter = newsletter;
        }

        /// <summary>
        /// Subscribe an email to the newsletter.
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<ActionResult<ServiceResponse>> Subscribe([FromBody] SubscribeEmailDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new ServiceResponse(false, "Email is required"));

            var result = await _newsletter.SubscribeAsync(dto.Email);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }

    public class SubscribeEmailDto { public string Email { get; set; } = string.Empty; }
}
