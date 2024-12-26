namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;

    using Microsoft.AspNetCore.Components.Forms;

    public interface IFileUploadService
    {
        Task<FileUploadResponse> UploadFileAsync(IBrowserFile file);
    }
}
