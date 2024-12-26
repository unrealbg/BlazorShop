namespace BlazorShop.Web.Shared.Models
{
    public record FileUploadResponse(bool Success = false, string Message = null!, string Url = null!);
}
