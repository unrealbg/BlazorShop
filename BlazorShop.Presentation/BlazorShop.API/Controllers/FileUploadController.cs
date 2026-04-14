namespace BlazorShop.API.Controllers
{
    using BlazorShop.API.Validation;
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
        private static readonly Dictionary<string, string> AllowedExtensionsByContentType = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
            ["image/bmp"] = ".bmp"
        };

        public FileUploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public sealed class ImageUploadForm
        {
            public IFormFile? File { get; set; }
        }

        /// <summary>
        ///  Uploads an image to the server and returns its URL.
        ///  DEBUG: absolute URL (https://localhost:port/uploads/...)
        ///  RELEASE: relative URL (/uploads/...).
        /// </summary>
        /// <param name="form">Image file to upload (multipart/form-data)</param>
        /// <returns>A message and the URL of the uploaded file.</returns>
        [HttpPost("image")]
        [Authorize(Roles = "User, Admin")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile([FromForm] ImageUploadForm form)
        {
            var file = form?.File;
            if (file == null || file.Length == 0)
            {
                return this.BadRequest(new FileUploadResponse(false, "No file uploaded.", null!));
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return this.BadRequest(new FileUploadResponse(false, $"File too large. Max {(MaxFileSizeBytes / (1024 * 1024))}MB.", null!));
            }

            if (!AllowedExtensionsByContentType.TryGetValue(file.ContentType, out var safeExt))
            {
                return this.BadRequest(new FileUploadResponse(false, "Invalid file type. Only image files are allowed.", null!));
            }

            await using (var validationStream = file.OpenReadStream())
            {
                var isValidImage = await ImageFileSignatureValidator.IsValidAsync(validationStream, file.ContentType, HttpContext.RequestAborted);
                if (!isValidImage)
                {
                    return this.BadRequest(new FileUploadResponse(false, "Invalid image content. The uploaded file does not match its declared type.", null!));
                }
            }

            var uploadsPath = Path.Combine(_environment.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            var uniqueName = $"{Guid.NewGuid():N}{safeExt}";
            var filePath = Path.Combine(uploadsPath, uniqueName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream, HttpContext.RequestAborted);
            }

            string fileUrl;
#if DEBUG
            fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueName}";
#else
            fileUrl = $"/uploads/{uniqueName}";
#endif

            return this.Ok(new FileUploadResponse(true, "File uploaded successfully.", fileUrl));
        }
    }
}
