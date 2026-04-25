namespace BlazorShop.Tests.Presentation.Services
{
    using System.Net;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models;

    using Xunit;

    public class FeedbackMessageResolverTests
    {
        [Fact]
        public void ResolveQueryFailure_WhenUnauthorized_ReturnsSessionMessage()
        {
            var result = QueryResult<object>.Failed("ignored", HttpStatusCode.Unauthorized);

            var message = FeedbackMessageResolver.ResolveQueryFailure(result, "fallback");

            Assert.Equal("Your session has expired. Sign in again and retry.", message);
        }

        [Fact]
        public void ResolveQueryFailure_WhenServiceUnavailable_ReturnsAvailabilityMessage()
        {
            var result = QueryResult<object>.Failed("ignored", HttpStatusCode.ServiceUnavailable);

            var message = FeedbackMessageResolver.ResolveQueryFailure(result, "fallback");

            Assert.Equal("The service is temporarily unavailable. Try again in a moment.", message);
        }

        [Fact]
        public void ResolveMutation_WhenValidationMessageExists_PreservesServerMessage()
        {
            var response = new ServiceResponse<object>(Message: "Email is already in use")
            {
                ResponseType = ServiceResponseType.ValidationError,
            };

            var message = FeedbackMessageResolver.ResolveMutation(response);

            Assert.Equal("Email is already in use", message);
        }

        [Fact]
        public void ResolveMutation_WhenFailureHasNoMessage_UsesTypedFallback()
        {
            var response = new ServiceResponse<object>()
            {
                ResponseType = ServiceResponseType.Conflict,
            };

            var message = FeedbackMessageResolver.ResolveMutation(response);

            Assert.Equal("This data changed while you were editing it. Refresh and try again.", message);
        }
    }
}