namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/upload")]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public FileUploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        /// <summary>
        ///  Uploads a file to the server.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>A message and the URL of the uploaded file.</returns>
        [HttpPost("image")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return this.BadRequest("No file uploaded.");
            }

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\uploads");

            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var filePath = Path.Combine(uploadsPath, file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{file.FileName}";

            return this.Ok(
                new FileUploadResponse { Success = true, Message = "File uploaded successfully.", Url = fileUrl });
        }
    }
}
