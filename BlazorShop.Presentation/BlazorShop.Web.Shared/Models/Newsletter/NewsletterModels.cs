namespace BlazorShop.Web.Shared.Models.Newsletter
{
    public record SubscribeRequest(string Email);

    public record SubscribeResponse(bool Success, string Message);
}
