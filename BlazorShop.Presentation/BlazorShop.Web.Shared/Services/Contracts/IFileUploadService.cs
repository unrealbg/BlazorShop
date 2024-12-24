namespace BlazorShop.Web.Shared.Services.Contracts
{
    using Microsoft.AspNetCore.Components.Forms;

    public interface IFileUploadService
    {
        Task<string?> UploadFileAsync(IBrowserFile file);
    }
}
