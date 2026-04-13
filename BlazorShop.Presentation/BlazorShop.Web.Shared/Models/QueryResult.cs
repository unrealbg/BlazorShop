namespace BlazorShop.Web.Shared.Models
{
    using System.Net;

    public sealed record QueryResult<T>(bool Success, T? Data = default, string Message = "", HttpStatusCode? StatusCode = null)
    {
        public static QueryResult<T> Succeeded(T data)
        {
            return new(true, data);
        }

        public static QueryResult<T> Failed(string message, HttpStatusCode? statusCode = null)
        {
            return new(false, default, message, statusCode);
        }
    }
}