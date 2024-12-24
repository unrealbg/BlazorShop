namespace BlazorShop.Web.Shared.Services
{
    using System.Net.Http.Headers;
    using System.Text.Json.Serialization;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Microsoft.AspNetCore.Components.Forms;

    public class FileUploadService : IFileUploadService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public FileUploadService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<string?> UploadFileAsync(IBrowserFile file)
        {
            var privateClient = await _httpClientHelper.GetPrivateClientAsync();

            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

            content.Add(streamContent, "file", file.Name);

            var apiCall = new ApiCall
            {
                Route = Constant.File.Upload,
                Type = Constant.ApiCallType.Post,
                Client = privateClient,
                Id = null!,
                Model = content
            };

            var result = await _apiCallHelper.ApiCallTypeCall<MultipartFormDataContent>(apiCall);

            if (result != null && result.IsSuccessStatusCode)
            {
                var response = await _apiCallHelper.GetServiceResponse<UploadedFileResult>(result);
                return response?.FileUrl;
            }

            throw new Exception("File upload failed");
        }

        private record UploadedFileResult(
            [property: JsonPropertyName("message")] string Message,
            [property: JsonPropertyName("fileUrl")] string FileUrl);
    }
}
