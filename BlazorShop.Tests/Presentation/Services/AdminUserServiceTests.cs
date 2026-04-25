namespace BlazorShop.Tests.Presentation.Services
{
    using System.Net;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Users;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class AdminUserServiceTests
    {
        [Fact]
        public async Task GetUsersAsync_UsesAdminUsersRouteWithFilters()
        {
            var httpClientHelper = new Mock<IHttpClientHelper>();
            var apiCallHelper = new Mock<IApiCallHelper>();
            var client = new HttpClient();
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            ApiCall? capturedCall = null;

            httpClientHelper.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            apiCallHelper
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .Callback<ApiCall>(call => capturedCall = call)
                .ReturnsAsync(response);
            apiCallHelper
                .Setup(helper => helper.GetQueryResult<PagedResult<AdminUserListItem>>(response, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<PagedResult<AdminUserListItem>>.Succeeded(new PagedResult<AdminUserListItem>()));

            var service = new AdminUserService(httpClientHelper.Object, apiCallHelper.Object);

            await service.GetUsersAsync(new AdminUserQuery
            {
                SearchTerm = "admin@example.com",
                Role = "Admin",
                Locked = false,
                PageNumber = 2,
                PageSize = 10,
            });

            Assert.NotNull(capturedCall);
            Assert.StartsWith("admin/users?", capturedCall!.Route);
            Assert.Contains("searchTerm=admin%40example.com", capturedCall.Route);
            Assert.Contains("role=Admin", capturedCall.Route);
            Assert.Contains("locked=false", capturedCall.Route);
            Assert.Equal(Constant.ApiCallType.Get, capturedCall.Type);
        }
    }
}
