namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/upload")]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        private const long MaxFileSizeBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
            "image/bmp"
        };

        public FileUploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        /// <summary>
        ///  Uploads an image to the server and returns its relative URL (e.g. /uploads/{file}).
        /// </summary>
        /// <param name="file">Image file to upload (multipart/form-data)</param>
        /// <returns>A message and the URL of the uploaded file.</returns>
        [HttpPost("image")]
        [Authorize(Roles = "User, Admin")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return this.BadRequest(new FileUploadResponse(false, "No file uploaded.", null!));
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return this.BadRequest(new FileUploadResponse(false, $"File too large. Max {(MaxFileSizeBytes / (1024 * 1024))}MB.", null!));
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                return this.BadRequest(new FileUploadResponse(false, "Invalid file type. Only image files are allowed.", null!));
            }

            var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".bin";
            }
            var safeExt = ext.ToLowerInvariant();
            var uniqueName = $"{Guid.NewGuid():N}{safeExt}";
            var filePath = Path.Combine(uploadsPath, uniqueName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{uniqueName}";

            return this.Ok(new FileUploadResponse(true, "File uploaded successfully.", fileUrl));
        }
    }
}
