namespace BlazorShop.Web.Shared.Models
{
    public enum ServiceResponseType
    {
        None = 0,
        Success = 1,
        ValidationError = 2,
        NotFound = 3,
        Conflict = 4,
        Failure = 5,
    }
}