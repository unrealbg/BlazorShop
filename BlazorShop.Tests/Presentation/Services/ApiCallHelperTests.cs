namespace BlazorShop.Tests.Presentation.Services
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;

    using Xunit;

    public class ApiCallHelperTests
    {
        [Fact]
        public async Task GetMutationResponse_ShouldReturnStructuredConflictResponse_WhenPayloadIsProvided()
        {
            var helper = new ApiCallHelper();
            var expected = new ServiceResponse<GetProductSeo>(Message: "Slug already exists")
            {
                ResponseType = ServiceResponseType.Conflict,
            };

            using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(expected),
            };

            var result = await helper.GetMutationResponse<GetProductSeo>(response, "Default error");

            Assert.False(result.Success);
            Assert.Equal("Slug already exists", result.Message);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
        }

        [Fact]
        public async Task GetMutationResponse_ShouldReturnSuccessType_WhenSuccessPayloadOmitsResponseType()
        {
            var helper = new ApiCallHelper();
            var expected = new ServiceResponse<GetSeoSettings>(Success: true, Message: "Saved");

            using var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected),
            };

            var result = await helper.GetMutationResponse<GetSeoSettings>(response, "Default error");

            Assert.True(result.Success);
            Assert.Equal("Saved", result.Message);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
        }
    }
}